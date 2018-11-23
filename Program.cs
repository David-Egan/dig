using System;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Net.NetworkInformation;
using System.Collections.Generic;

namespace dig
{
    enum DnsType
    {
        A,
        AAAA
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Program prog = new Program();


            string hostname = "mail.google.com";
            IPAddress defaultDns = prog.FindDefaultDNS();
            prog.SendRequest(hostname, DnsType.AAAA, defaultDns);

        }

        private void SendRequest(string hostname, DnsType type, IPAddress defaultDns)
        {
            //var data = "ger";
            //byte[] data = new byte[74];
            //data[0] = 0x2c;

            List<byte> byteList = new List<byte>();

            Byte[] data = ToByteArray("387101000001000000000000076578616d706c6503636f6d00001c0001");
            //data[]

            // 0-1 Transaction ID
            byteList.Add(0x38);
            byteList.Add(0x71);
            data[0] = 0x38;
            data[1] = 0x71;

            // 2-3 Flags: Standard Query is 0100
            byteList.Add(0x01);
            byteList.Add(0x00);
            data[2] = 0x01;
            data[3] = 0x00;

            // 4-5 Question Count (I think always gonna be 0001)
            byteList.Add(0x00);
            byteList.Add(0x01);
            data[4] = 0x00;
            data[5] = 0x01;

            // 6-7 Answer Counts (always 0 for requests)
            byteList.Add(0x00);
            byteList.Add(0x00);
            data[6] = 0x00;
            data[7] = 0x00;

            // 8-9 Authority Counts (always 0 for requests)
            byteList.Add(0x00);
            byteList.Add(0x00);
            data[8] = 0x00;
            data[9] = 0x00;

            // 10-11 Additional Counts (always 0 for requests)
            byteList.Add(0x00);
            byteList.Add(0x00);
            data[10] = 0x00;
            data[11] = 0x00;

            // The Rest is Queries section
            //

            // Name: however long it needs to be ALWAYS ends in 00 SOMETIMES starts with value that isn't letter
            string[] hostnameComponents = hostname.Split(".");
            List<byte> hostnameBytes = new List<byte>();


            foreach (string s in hostnameComponents)
            {
                int slen = s.Length;


                hostnameBytes.Add((byte)(slen));
                hostnameBytes.AddRange(System.Text.Encoding.ASCII.GetBytes(s)); 


            }


            //byte[] hostnameBytes = System.Text.Encoding.ASCII.GetBytes(hostname);
            byteList.AddRange(hostnameBytes);
            byteList.Add(0x00);



            // Type: 2 bytes 0001 is Type A, 001c is Type AAAA
            if (type == DnsType.A)
            {
                byteList.Add(0x00);
                byteList.Add(0x01);
            } else if (type == DnsType.AAAA)
            {
                byteList.Add(0x00);
                byteList.Add(0x1c);
            }

            
            // Class: 2 bytes maybe doesnt matter IN is 0001
            byteList.Add(0x00);
            byteList.Add(0x01);


            Udp(byteList.ToArray(), defaultDns);
        }

        public static Byte[] ToByteArray(String hexString)
        {
            Byte[] retval = new Byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
                retval[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            return retval;
        }


        async void Udp(Byte[] data, IPAddress dnsAddress)
        {
            try
            {
                var client = new UdpClient(5080);
                //client.
                for (; ; )
                {

                    // find dns server
                    //System.
                    //IPAddress defaultDNS = FindDefaultDNS();

                    //IPAddress serverAddr = IPAddress.Parse("192.168.2.255");
                    //IPAddress serverAddr = IPAddress.Parse("209.18.47.61");

                    //string serverStr = System.Text.Encoding.ASCII.GetString(dnsAddress.GetAddressBytes());
//                    IPAddress serverAddr = IPAddress.Parse(d);

                    var ep = new System.Net.IPEndPoint(dnsAddress, 53);//11000);
                    


                    System.Console.WriteLine(ep.Address.ToString() + " " + ep.Port);

                    //var data = DateTime.Now.ToString();
                    var msg = data;//System.Text.Encoding.ASCII.GetBytes(data);
                                  
                    var i = await client.SendAsync(msg, msg.Length, ep);

                    var response = await client.ReceiveAsync(); 
                    Console.WriteLine("response received from " + response.RemoteEndPoint);
                    Thread.Sleep(10000);
                    
                    
                    System.Console.WriteLine(i + " bytes sent");
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                System.Console.WriteLine("UDP Client failed");
            }
        }

        private IPAddress FindDefaultDNS()
        {
            Boolean isValid; // = false;

            foreach (var netI in NetworkInterface.GetAllNetworkInterfaces())
            {
                isValid = false;

                if (netI.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    (netI.NetworkInterfaceType != NetworkInterfaceType.Ethernet ||
                     netI.OperationalStatus != OperationalStatus.Up)) continue;
                foreach (var uniIpAddrInfo in netI.GetIPProperties().UnicastAddresses.Where(x => netI.GetIPProperties().GatewayAddresses.Count > 0))
                {



                    if ((uniIpAddrInfo.Address.AddressFamily == AddressFamily.InterNetwork || uniIpAddrInfo.Address.AddressFamily == AddressFamily.InterNetworkV6) &&
                        uniIpAddrInfo.AddressPreferredLifetime != uint.MaxValue) { 

                        isValid = true;
                        break;
                    }
                    
                }

                if (isValid)
                {
                    foreach (IPAddress dnsAdd in netI.GetIPProperties().DnsAddresses)
                    {
                        return dnsAdd;
                    }
                    //IPAddress ipAddr = netI.GetIPProperties().DnsAddresses;
                }

            }

            throw new Exception("Could not find DNS Server");
        }


            /*
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    IPAddressCollection dnsAddresses = ni.GetIPProperties().DnsAddresses;

                    foreach (IPAddress dnsAddress in dnsAddresses.Where(ip => ( ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) || (ip.AddressFamily == AddressFamily.InterNetworkV6)    ))
                    {

                       
                        try
                        {
                            new System.Net.IPEndPoint(dnsAddress, 53);
                        }
                        catch
                        {
                            continue;
                        }

                        
                        return dnsAddress;
                    }
                }
            }

            throw new InvalidOperationException("Unable to find DNS Address");
            */
        
       
    }
}
