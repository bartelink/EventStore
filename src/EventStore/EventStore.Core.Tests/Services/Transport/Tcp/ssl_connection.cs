﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Tests.Helper;
using EventStore.Transport.Tcp;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Tcp
{
    [TestFixture]
    public class ssl_connections
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ssl_connections>();

        [Test]
        public void should_connect_to_each_other_and_send_data()
        {
            var ip = IPAddress.Loopback;
            var port = TcpPortsHelper.GetAvailablePort(ip);
            var serverEndPoint = new IPEndPoint(ip, port);
            var cert = GetCertificate();

            var sent = new byte[1000];
            new Random().NextBytes(sent);

            var received = new MemoryStream();

            var done = new ManualResetEventSlim();

            var listener = new TcpServerListener(serverEndPoint);
            listener.StartListening((endPoint, socket) =>
            {
                var ssl = TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, cert, verbose: true);
                Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
                callback = (x, y) =>
                {
                    foreach (var arraySegment in y)
                    {
                        received.Write(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
                    }

                    if (received.Length >= sent.Length)
                        done.Set();
                    else
                        ssl.ReceiveAsync(callback);
                };
                ssl.ReceiveAsync(callback);
            });

            var clientSsl = TcpConnectionSsl.CreateConnectingConnection(
                Guid.NewGuid(), 
                serverEndPoint, 
                "ES",
                false,
                new TcpClientConnector(),
                conn =>
                {
                    conn.EnqueueSend(new[] {new ArraySegment<byte>(sent)});
                },
                (conn, err) =>
                {
                    Log.Error("Connecting failed: {0}.", err);
                    done.Set();
                },
                verbose: true);

            done.Wait();

            listener.Stop();
            clientSsl.Close("Normal close.");

            Assert.AreEqual(sent, received.ToArray());
        }

        private X509Certificate GetCertificate()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EventStore.Core.Tests.server.p12"))
            using (var mem = new MemoryStream())
            {
                stream.CopyTo(mem);
                return new X509Certificate2(mem.ToArray(), "1111");
            }
        }
    }
}
