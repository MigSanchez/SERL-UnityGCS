// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AudioStream
{
    public class IcecastWriter
    {
        NetworkStream networkStream;
        StreamReader streamReader;
        StreamWriter streamWriter;
        TcpClient tcpClient;

        public IcecastWriter()
        {
            Hostname = "127.0.0.1";
            Port = 8000;
            Mountpoint = "/stream"; // default
            Username = "source";
            Password = "hackme";
            ContentType = "audio/unspecified";  /* will be defined on first packet */
            Name = "";
            Description = "";
            Genre = "";
            Url = "";
            // TODO: bitrate derived from source? seems to be computed by Icecast automatically.
            // KBitrate = 128;
            Public = false;
            UserAgent = "AudioStream " + About.version;
        }

        /// <summary>
        /// listen-socket bind-address
        /// </summary>
        public string Hostname { get; set; }
        /// <summary>
        /// listen-socket port
        /// </summary>
        public ushort Port { get; set; }
        /// <summary>
        /// listen-socket shoutcast-mount
        /// </summary>
        public string Mountpoint { get; set; }
        /// <summary>
        /// authentication
        /// Sources log in with username 'source'
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// authentication source-password
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ContentType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Genre { get; set; }
        /// <summary>
        /// Icy-url announce. Can be whatever.
        /// </summary>
        public string Url { get; set; }
        // TODO: bitrate derived from source? seems to be computed by Icecast automatically.
        /// <summary>
        /// 
        /// </summary>
        // public ushort KBitrate { get; set; }
        /// <summary>
        /// Is this for announce in global directory ?
        /// </summary>
        public bool Public { get; set; }
        /// <summary>
        /// Connection user agent
        /// </summary>
        public string UserAgent { get; set; }

        public bool Connected
        {
            get { return this.networkStream != null && this.streamReader != null && this.streamWriter != null && this.tcpClient != null && this.tcpClient.Connected; }
        }

        public bool Open()
        {
            try
            {
                this.tcpClient = new TcpClient(Hostname, Port);
                this.networkStream = this.tcpClient.GetStream();
                this.streamReader = new StreamReader(this.networkStream);
                this.streamWriter = new StreamWriter(this.networkStream) { AutoFlush = true };

                // Request headers
                // https://gist.github.com/ePirat/adc3b8ba00d85b7e3870
                // Icecast 2.4.0 +

                this.streamWriter.WriteLine("PUT {0} HTTP/1.1", Mountpoint);
                this.streamWriter.WriteLine("Host: {0}", string.Format("{0}:{1}", Hostname, Port));
                this.streamWriter.WriteLine("Authorization: Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}", Username, Password))));
                this.streamWriter.WriteLine("User-Agent: {0}", this.UserAgent);
                this.streamWriter.WriteLine("Accept: {0}", "*/*");
                this.streamWriter.WriteLine("Transfer-Encoding: {0}", "chunked");
                this.streamWriter.WriteLine("Content-Type: {0}", ContentType);
                this.streamWriter.WriteLine("Ice-Public: {0}", Public ? 1 : 0);
                this.streamWriter.WriteLine("Ice-Name: {0}", Name);
                this.streamWriter.WriteLine("Ice-Description: {0}", Description);
                this.streamWriter.WriteLine("Ice-URL: {0}", Url);
                this.streamWriter.WriteLine("Ice-Genre: {0}", Genre);

                // TODO: bitrate derived from source? seems to be computed by Icecast automatically.
                //this.streamWriter.WriteLine("bitrate: {0}", KBitrate);
                //this.streamWriter.WriteLine("audio_bitrate: {0}", KBitrate * 1000);
                //this.streamWriter.WriteLine("ice-bitrate: {0}", KBitrate);
                //this.streamWriter.WriteLine("ice-audio-info: ice-bitrate={0}", KBitrate);

                this.streamWriter.WriteLine("ice-private: {0}", Public ? 0 : 1);

                this.streamWriter.WriteLine("Expect: 100-continue");
                this.streamWriter.WriteLine();

                // Authorized?
                string statusLine = this.streamReader.ReadLine();
                if (statusLine == null)
                {
                    UnityEngine.Debug.LogFormat("Icecast socket error: No response");
                    return false;
                }
                string[] status = statusLine.Split(' ');

                if (status[1] == "100")
                    // Now we can stream
                    return true;

                // Something went wrong
                UnityEngine.Debug.LogFormat("Icecast HTTP error: {0} {1}", status[1], status[2]);
                Close();
                return false;
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogErrorFormat("ICECASTWRITING: {0}", error);
                // Something went wrong
                Close();
                return false;
            }
        }

        public void Push(byte[] data)
        {
            try
            {
                if (this.networkStream == null || this.streamReader == null || this.streamWriter == null || this.tcpClient == null || !this.tcpClient.Connected)
                    return;
                this.streamWriter.BaseStream.Write(data, 0, data.Length);
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogFormat("Icecast socket error while pushing data: {0}", error);
                Close();
            }
        }

        // TODO: test metadata
        /// <summary>
        /// 
        /// </summary>
        /// <param name="song"></param>
        /// <param name="tryOnce"></param>
        /// <returns></returns>
        public bool SendMetadata(string song, bool tryOnce = false)
        {
            if (song == null)
                song = string.Empty;

            var reqQuery = new Dictionary<string, string>
            {
                //{"pass",Password},
                {"mode", "updinfo"},
                {"mount", Mountpoint},
                {"song", song}
            };

            var reqUriBuilder = new UriBuilder("http", Hostname, Port, "/admin/metadata")
            {
                Query =
                    string.Join("&",
                        reqQuery.Keys.Select(
                            key =>
                                string.Format("{0}={1}", Uri.EscapeUriString(key),
                                    Uri.EscapeUriString(reqQuery[key]))).ToArray()),
                //UserName = Username,
                //Password = Password
            };

            var req = (HttpWebRequest)WebRequest.Create(reqUriBuilder.Uri);
            req.UserAgent = this.UserAgent;
            req.Credentials = new NetworkCredential(Username, Password);

            try
            {
                req.GetResponse();
                return true;
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogWarningFormat("Uri: {0}", reqUriBuilder.Uri);
                UnityEngine.Debug.LogWarningFormat("Could not send metadata: {0}", error.Message);
                if (tryOnce)
                    return false;

                Thread.Sleep(1000);
                return SendMetadata(song, true);
            }
        }

        public void Close()
        {
            UnityEngine.Debug.Log("Disconnecting");
            if (this.networkStream == null || this.streamReader == null || this.streamWriter == null || this.tcpClient == null || !this.tcpClient.Connected)
                return;

            this.streamReader.Dispose();
            this.streamWriter.Dispose();
            this.networkStream.Dispose();
            this.tcpClient.Close();
        }
    }
}