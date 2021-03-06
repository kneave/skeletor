﻿using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += timer_Elapsed;
            timer.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        static void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            WhosThere();
        }

        private static void WhosThere()
        {
            string request = "Who's there?";
            Console.WriteLine("asking {0}", request);

            Console.WriteLine(SendRequest(request));
        }

        public static string SendRequest(string request, int timeout = 5000)
        {
            string joshuaHost = "10.30.16.16";
            string joshuaPort = "2804";
            //logHandler.WriteLog(string.Format("Sent request: {0}", request));
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket clientSocket = context.CreateRequestSocket())
                {
                    try
                    {
                        clientSocket.Connect(string.Format("tcp://{0}:{1}", joshuaHost, joshuaPort));
                        clientSocket.Options.ReceiveTimeout = TimeSpan.FromMilliseconds(timeout);

                        clientSocket.Send(request);
                        //logHandler.WriteLog(string.Format("Returning {0} bytes", bytes.Length));
                        return clientSocket.ReceiveString();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(string.Format("Exception getting data: {0}", e));
                        return null;
                    }
                }
            }
        }
    }
}
