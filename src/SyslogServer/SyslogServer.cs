﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WatsonSyslog
{
    /// <summary>
    /// Watson syslog server.
    /// </summary>
    public partial class SyslogServer
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static string _Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private static string _SettingsContents = "";
        private static Settings _Settings = new Settings();
        private static Thread _ListenerThread;
        private static UdpClient _ListenerUdp;
        private static TcpListener _ListenerTcp;

        private static DateTime _LastWritten = DateTime.Now;
        private static List<string> _MessageQueue = new List<string>();
        private static readonly object _WriterLock = new object();

        #endregion

        #region Public-Methods

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

            #region Welcome

            Console.WriteLine("---");
            Console.WriteLine("Watson Syslog Server | v" + _Version);
            Console.WriteLine("(c)2022 Joel Christner, 2024 Richard Hasha");
            Console.WriteLine("https://github.com/jchristn/watsonsyslogserver");
            Console.WriteLine("---");

            #endregion

            #region Read-Config-File

            if (File.Exists("syslog.json"))
            {
                _SettingsContents = Encoding.UTF8.GetString(File.ReadAllBytes("syslog.json"));
            } 

            if (String.IsNullOrEmpty(_SettingsContents))
            {
                Console.WriteLine("Unable to read syslog.json, using default configuration:");
                Console.WriteLine(_Settings.ToString());
            }
            else
            {
                try
                {
                    _Settings = Common.DeserializeJson<Settings>(_SettingsContents);
                }
                catch (Exception)
                {
                    Console.WriteLine("Unable to deserialize syslog.json, please check syslog.json for correctness, exiting");
                    throw;
                    // Environment.Exit(-1);
                }
            }

            if (!Directory.Exists(_Settings.LogFileDirectory)) Directory.CreateDirectory(_Settings.LogFileDirectory);

            #endregion
            
            #region Start-Server

            StartServer();

            #endregion

            #region Console
             
            while (true)
            {
                string userInput = Common.InputString("[syslog :: ? for help] >", null, false);
                switch (userInput)
                {
                    case "?":
                        Console.WriteLine("---");
                        Console.WriteLine("  q      quit the application");
                        Console.WriteLine("  cls    clear the screen");
                        break;

                    case "q": 
                        Console.WriteLine("Exiting.");
                        Environment.Exit(0);
                        break;

                    case "c":
                    case "cls":
                        Console.Clear();
                        break;
                            
                    default:
                        Console.WriteLine("Unknown command.  Type '?' for help.");
                        continue;
                }
            }
                  
            #endregion
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled exception occurred, application will restart after delay.");
            Exception ex = (Exception)e.ExceptionObject;
            Console.WriteLine("Exception: " + ex.ToString());

            StopServer();

            // Delay before restart
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10));

            // Start a new instance of the application
            string x = System.Reflection.Assembly.GetExecutingAssembly().Location;
            x = x.Substring(0, x.Length - 4) + ".exe";
            var y = System.Diagnostics.Process.Start(x);

            // Terminate the current application
            Environment.Exit(1);
        }
        #endregion

        #region Private-Methods

        private static volatile bool StopWriterTask;

        private static void StartServer()
        {
            try
            {
                Console.WriteLine("Starting at " + DateTime.Now);

                _ListenerThread = new Thread(ReceiverThread);
                _ListenerThread.Start();
                Console.WriteLine("Listening on UDP/" + _Settings.UdpPort + ".");

                _ListenerTcp = new TcpListener(IPAddress.Any, _Settings.UdpPort);       // Same port is used for both UDP and TCP
                _ListenerTcp.Start();
                Thread listenerThreadTcp = new Thread(ReceiverThreadTcp);
                listenerThreadTcp.Start();
                Console.WriteLine("Listening on TCP/" + _Settings.UdpPort + ".");

                StopWriterTask = false;
                Task.Run(() => WriterTask());
                Console.WriteLine("Writer thread started successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine("***");
                Console.WriteLine("Exiting due to exception: " + e.Message);
                UnhandledExceptionHandler(null, new UnhandledExceptionEventArgs(e, false));
                throw;
                // Environment.Exit(-1);
            }
        }

        private static void StopServer()
        {
            try
            {
                // Stop the UDP listener thread
                _ListenerThread.Interrupt();
                _ListenerThread.Join();
                Console.WriteLine("Stopped listening on UDP/" + _Settings.UdpPort + ".");
            }
            catch (Exception e)
            {
                Console.WriteLine("***");
                Console.WriteLine("Error while stopping the server: " + e.Message);
            }

            try
            {
                // Stop the TCP listener
                _ListenerTcp.Stop();
                Console.WriteLine("Stopped listening on TCP/" + _Settings.UdpPort + ".");
            }
            catch (Exception e)
            {
                Console.WriteLine("***");
                Console.WriteLine("Error while stopping the server: " + e.Message);
            }

            try
            {
                // Stop the writer task
                StopWriterTask = true;
                Thread.Sleep(4000);
                Console.WriteLine("Writer thread stopped successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine("***");
                Console.WriteLine("Error while stopping the server: " + e.Message);
            }
        }

        private static void ReceiverThread()
        {
            if (_ListenerUdp == null) _ListenerUdp = new UdpClient(_Settings.UdpPort);

            try
            {
                #region Start-Listener

                IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, _Settings.UdpPort);
                string receivedData;
                byte[] receivedBytes;

                while (true)
                {
                    #region Receive-Data

                    receivedBytes = _ListenerUdp.Receive(ref endpoint);
                    receivedData = Encoding.ASCII.GetString(receivedBytes, 0, receivedBytes.Length);
                    string msg = null;
                    if (_Settings.DisplayTimestamps) msg = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + " ";
                    msg += receivedData;
                    Console.WriteLine(msg);

                    #endregion

                    #region Add-to-Queue

                    lock (_WriterLock)
                    {
                        _MessageQueue.Add(msg);
                    }

                    #endregion
                }

                #endregion
            }
            catch (Exception e)
            {
                _ListenerUdp.Close();
                _ListenerUdp = null;
                Console.WriteLine("***");
                Console.WriteLine("ReceiverThread exiting due to exception: " + e.Message);
                return;
            }
        }

        private static void ReceiverThreadTcp()
        {
            try
            {
                while (true)
                {
                    TcpClient client = _ListenerTcp.AcceptTcpClient();
                    Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception e)
            {
                _ListenerTcp.Stop();
                Console.WriteLine("***");
                Console.WriteLine("ReceiverThreadTcp exiting due to exception: " + e.Message);
                return;
            }
        }

        private static void HandleClient(TcpClient client)
        {
            try
            {
                using (StreamReader reader = new StreamReader(client.GetStream(), Encoding.ASCII))
                {
                    while (client.Connected)
                    {
                        // Read the length of the message
                        string lengthStr = "";
                        char ch;
                        while ((ch = (char)reader.Read()) != ' ')
                        {
                            lengthStr += ch;
                        }

                        // Convert the length to an integer
                        int length = int.Parse(lengthStr);

                        // Read the message
                        char[] buffer = new char[length];
                        reader.Read(buffer, 0, length);
                        string receivedData = new string(buffer);

                        string msg = null;
                        if (_Settings.DisplayTimestamps) msg = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + " ";
                        msg += receivedData;
                        Console.WriteLine(msg);

                        lock (_WriterLock)
                        {
                            _MessageQueue.Add(msg);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("***");
                Console.WriteLine("HandleClient exiting due to exception: " + e.Message);
            }
            finally
            {
                client.Close();
            }
        }
        static void WriterTask()
        {
            try
            {
                while (true)
                {
                    if (StopWriterTask) 
                    {
                        return;
                    }
                    Task.Delay(1000).Wait();

                    if (DateTime.Compare(_LastWritten.AddSeconds(_Settings.LogWriterIntervalSec), DateTime.Now) < 0)
                    {
                        lock (_WriterLock)
                        {
                            if (_MessageQueue == null || _MessageQueue.Count < 1)
                            {
                                _LastWritten = DateTime.Now;
                                continue;
                            }

                            foreach (string currMessage in _MessageQueue)
                            {
                                string currFilename = _Settings.LogFileDirectory + DateTime.Now.ToString("MMddyyyy") + "-" + _Settings.LogFilename;

                                if (!File.Exists(currFilename))
                                {
                                    // Delete all old files (more than MaxLogFileAgeInDays days old) - assuming the file name as generated above
                                    string[] files = Directory.GetFiles(_Settings.LogFileDirectory, "*-log.txt");
                                    foreach (string file in files)
                                    {
                                        string dateStr = file.Substring(file.LastIndexOf("-") - 8, 8);
                                        DateTime fileDate = DateTime.ParseExact(dateStr, "MMddyyyy", null);
                                        if (DateTime.Compare(fileDate.AddDays(_Settings.MaxLogFileAgeInDays), DateTime.Now) < 0)
                                        {
                                            Console.WriteLine("Deleting file: " + file + Environment.NewLine);
                                            File.Delete(file);
                                        }
                                    }


                                    Console.WriteLine("Creating file: " + currFilename + Environment.NewLine);
                                    {
                                        using (FileStream fsCreate = File.Create(currFilename))
                                        {
                                            Byte[] createData = new UTF8Encoding(true).GetBytes("--- Creating log file at " + DateTime.Now + " ---" + Environment.NewLine);
                                            fsCreate.Write(createData, 0, createData.Length);
                                        }
                                    }
                                }

                                using (StreamWriter swAppend = File.AppendText(currFilename))
                                {
                                    swAppend.WriteLine(currMessage);
                                }
                            }

                            _LastWritten = DateTime.Now;
                            _MessageQueue = new List<string>();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("***");
                Console.WriteLine("WriterTask exiting due to exception: " + e.Message);
                UnhandledExceptionHandler(null, new UnhandledExceptionEventArgs(e, false));
                throw;
                // Environment.Exit(-1);
            }
        }

        #endregion
    }
}
