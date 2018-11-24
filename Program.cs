using System;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Text;

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


            string hostname = "rit.edu";
            IPAddress defaultDns = prog.FindDefaultDNS();
            prog.SendRequest(hostname, DnsType.A, defaultDns);

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


            Udp(byteList.ToArray(), defaultDns, hostname);

            Console.ReadKey();
        }

        public static Byte[] ToByteArray(String hexString)
        {
            Byte[] retval = new Byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
                retval[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            return retval;
        }

        private void ReadResponse(UdpReceiveResult result, string hostname)
        {
            byte[] resultBuffer = result.Buffer;


            // 0-1 Transaction ID
            // DONT CARE

            // 2-3 Flags: Standard Query is 0100
            // DONT CARE

            // 4-5 Question Count should always be 0001
            // DONT CARE

            // 6-7 Answer Counts (always 0 for requests)

    
            Int32 answerCount = TranslateBytes(resultBuffer, 6, 2);

            Console.WriteLine("Answer Count is: " + answerCount);


            // 8-9 Authority Counts (always 0 for requests)
            // DONT CARE

            // 10-11 Additional Counts (always 0 for requests)
            // DONT CARE

            // The Rest is Queries section
            //

            // Name: however long it needs to be ALWAYS ends in 00 SOMETIMES starts with value that isn't letter

            // current byte = 13 (12th index)
            hostname = hostname + "";

            Console.WriteLine("Hostname is: " + hostname);
                
            int currByte = 12;

            while (resultBuffer[currByte] != 0)
            {
                currByte++;
            }
            currByte++;            

            // Type: 2 bytes 0001 is Type A, 001c is Type AAAA
            DnsType type = 0;
            if (resultBuffer[currByte] == 0x00 && resultBuffer[currByte+1] == 0x01)
            {
                type = DnsType.A;
            }            
            else if (resultBuffer[currByte] == 0x00 && resultBuffer[currByte+1] == 0x1c)
            {
                type = DnsType.AAAA;
            }

            currByte += 2;
            Console.WriteLine("Type is: " + (type == DnsType.A ? "A" : "AAAA"));


            // Class: 2 bytes maybe doesnt matter IN is 0001           
            Int32 classVal = TranslateBytes(resultBuffer, currByte, 2); 

            Console.WriteLine("Class is: " + classVal + "  1 is IN");

            currByte += 2;

            while (currByte  < resultBuffer.Length)
            {
                // pointer detected!!!!!
                if (resultBuffer[currByte] == 0xc0)
                {
                    StringBuilder sb = new StringBuilder();

                    int runLengthLeft; //= currByte + 1;

                    int offset = resultBuffer[currByte + 1];
                    int pointer = offset; //= currByte + 2;

                    while (resultBuffer[pointer] != 0)
                    {
                        runLengthLeft = resultBuffer[pointer];
                        pointer++;

                        while (runLengthLeft > 0)   //resultBuffer[pointer] != 0)
                        {
                            sb.Append(Encoding.ASCII.GetString(new[] { resultBuffer[pointer] }));
                            runLengthLeft--;
                            pointer++;
                        }

                        if (resultBuffer[pointer] != 0) { sb.Append(".");  }
                     
                    }

                    Console.WriteLine(sb.ToString());
                    Thread.Sleep(5000);
                    currByte += 2;
                }
                else
                {
                    StringBuilder sb = new StringBuilder();

                    int runLengthLeft;
                    
                    int pointer = currByte; //= currByte + 2;

                    while (resultBuffer[pointer] != 0)
                    {
                        runLengthLeft = resultBuffer[pointer];
                        pointer++;

                        while (runLengthLeft > 0)   //resultBuffer[pointer] != 0)
                        {
                            sb.Append(Encoding.ASCII.GetString(new[] { resultBuffer[pointer] }));
                            runLengthLeft--;
                            pointer++;
                        }

                        if (resultBuffer[pointer] != 0) { sb.Append("."); }

                    }

                    currByte = pointer++;
                    Thread.Sleep(5000);
                    Console.WriteLine(sb.ToString());
                }


                // read in other garabage

                // read in answer            
                int answerType = TranslateBytes(resultBuffer, currByte, 2);
                currByte += 2;
                Console.WriteLine("Answer val: " + answerType );

                // read in class
                classVal = TranslateBytes(resultBuffer, currByte, 2);
                currByte += 2;
                Console.WriteLine("Class val: " + classVal);


                // time to live
                int ttl = TranslateBytes(resultBuffer, currByte, 4);
                currByte += 4;
                Console.WriteLine("time to live: " + ttl);

                // data length
                int dataLength = TranslateBytes(resultBuffer, currByte, 2);
                currByte += 2;
                Console.WriteLine("data length: " + dataLength);

                // address
                StringBuilder ipadd = new StringBuilder();
                for (int i = 0; i < dataLength; i++){                    
                    ipadd.Append((int)resultBuffer[currByte]);
                    ipadd.Append(".");
                    currByte++;
                }
                //int address = TranslateBytes(resultBuffer, currByte, dataLength);
                //currByte += dataLength;
                Console.WriteLine("compressed ip address: " + ipadd.ToString());



            }



        }

        private int TranslateBytes(byte[] sourceArr, int startIndex, int length)
        {
            byte[] targetBytes = new byte[length];

            Array.Copy(sourceArr, startIndex, targetBytes, 0, length);
            targetBytes = targetBytes.Reverse().ToArray();
            Int32 result = BitConverter.ToInt16(targetBytes);
            return result;
        }

        async void Udp(Byte[] data, IPAddress dnsAddress, string hostname)
        {
            try
            {
                var client = new UdpClient(5080);
                //client.
                for (; ; )
                {
                    var ep = new System.Net.IPEndPoint(dnsAddress, 53);
                    
                    System.Console.WriteLine(ep.Address.ToString() + " " + ep.Port);
                    
                    var msg = data;//System.Text.Encoding.ASCII.GetBytes(data);
                                  
                    var i = await client.SendAsync(msg, msg.Length, ep);

                    UdpReceiveResult response = await client.ReceiveAsync();
                    ReadResponse(response, hostname);

                    Thread.Sleep(5000);
                    Console.WriteLine("response received from " + response.RemoteEndPoint);         
                    
                    
                    System.Console.WriteLine(i + " bytes sent");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("UDP Client failed");
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
                }

            }

            throw new Exception("Could not find DNS Server");
        }
       
    }
}
