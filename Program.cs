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
        AAAA,
        CNAME
    }

    class Program
    {
        Dictionary<int, String> pointerList = new Dictionary<int, string>();


        static void Main(string[] args)
        {
            string hostname  = "";
            IPAddress dnsServer = null;
            DnsType dnsType = 0;

            Program prog = new Program();

            if (args.Length > 3 || args.Length == 0)
            {
                Console.WriteLine("Incorrect amont of arguments. Terminating program");
                return;
            } else if (args.Length == 1)
            {
                hostname = args[0];
                dnsType = DnsType.A;
                dnsServer = prog.FindDefaultDNS();
            } else if (args.Length == 2)
            {
                string typeString = args[0];
                hostname = args[1];
                dnsType = (typeString == "A") ? DnsType.A : DnsType.AAAA;
                dnsServer = prog.FindDefaultDNS();
            } else if (args.Length == 3)
            {
                dnsServer = IPAddress.Parse(args[0]);
                dnsType = (args[1] == "A") ? DnsType.A : DnsType.AAAA;
                hostname = args[2];
            }


            Console.WriteLine("David's DigLite");
           
            prog.SendRequest(hostname, dnsType, dnsServer);

        }

        private void SendRequest(string hostname, DnsType type, IPAddress defaultDns)
        {
            List<byte> byteList = new List<byte>();

            // 0-1 Transaction ID
            byteList.Add(0x38);
            byteList.Add(0x71);

            // 2-3 Flags: Standard Query is 0100
            byteList.Add(0x01);
            byteList.Add(0x00);

            // 4-5 Question Count (I think always gonna be 0001)
            byteList.Add(0x00);
            byteList.Add(0x01);

            // 6-7 Answer Counts (always 0 for requests)
            byteList.Add(0x00);
            byteList.Add(0x00);

            // 8-9 Authority Counts (always 0 for requests)
            byteList.Add(0x00);
            byteList.Add(0x00);

            // 10-11 Additional Counts (always 0 for requests)
            byteList.Add(0x00);
            byteList.Add(0x00);

            // The Rest is Queries section            
            // Name: however long it needs to be ALWAYS ends in 00 SOMETIMES starts with value that isn't letter
            string[] hostnameComponents = hostname.Split(".");
            List<byte> hostnameBytes = new List<byte>();


            foreach (string s in hostnameComponents)
            {
                int slen = s.Length;

                hostnameBytes.Add((byte)(slen));
                hostnameBytes.AddRange(System.Text.Encoding.ASCII.GetBytes(s)); 
            }

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


            SendUdpRequest(byteList.ToArray(), defaultDns, hostname, type);
        }

        public static Byte[] ToByteArray(String hexString)
        {
            Byte[] retval = new Byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
                retval[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            return retval;
        }

        private void ReadResponse(byte[] resultBuffer, string hostname)
        {
            Console.WriteLine();
            Console.WriteLine(";; ANSWER SECTION");            

            Int32 answerCount = TranslateBytes(resultBuffer, 6, 2);
            if (answerCount == 0)
            {
                Console.WriteLine("NO ANSWERS");
                return;
            }
          
            string[] hostnameSegments = hostname.Split(".");

            int currBytePos = 12;

            for (int i = 0; i < hostnameSegments.Length; i++)
            {
                pointerList[currBytePos] = hostnameSegments[i];
                currBytePos += hostnameSegments[i].Length + 1;
            }                        
                
            // skip through hostname data we already know
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
            
            // Class: 2 bytes maybe doesnt matter            
            currByte += 2;

            int currAnswer = 0;
            while (currAnswer < answerCount)
            {
                currAnswer += 1;

                int startOfString = currByte;
                string resultStr = ReadString(resultBuffer, ref currByte, int.MaxValue);                
               
                DnsType answerType;

                string dnsTypeString;
                if (resultBuffer[currByte] == 0x00 && resultBuffer[currByte+1] == 0x01) {
                    answerType = DnsType.A;
                    dnsTypeString = "A";
                }
                else if (resultBuffer[currByte] == 0x00 && resultBuffer[currByte+1] == 0x1c)
                {
                    answerType = DnsType.AAAA;
                    dnsTypeString = "AAAA";
                }
                else
                {
                    answerType = DnsType.CNAME;
                    dnsTypeString = "CNAME";
                }

                currByte += 2;           

                // skip in class
                currByte += 2;                

                // time to live
                int ttl = TranslateBytes(resultBuffer, currByte, 4);
                currByte += 4;                

                // data length
                int dataLength = TranslateBytes(resultBuffer, currByte, 2);
                currByte += 2;                

                // address
                StringBuilder ipadd;
                if (answerType == DnsType.A)
                {
                    ipadd = new StringBuilder();
                    for (int i = 0; i < dataLength; i++)
                    {
                        if (i != 0) { ipadd.Append(".");  }
                        ipadd.Append((int)resultBuffer[currByte]);                        
                        currByte++;
                    }
                }
                else if (answerType == DnsType.AAAA)
                {
                    ipadd = new StringBuilder();
                    for (int i = 0; i < dataLength; i ++)
                    {
                        if (i != 0) { ipadd.Append(":"); } 

                        string[] nextSegment = new string[2];
                        
                        string part1 = resultBuffer[currByte].ToString("x2");
                        string part2 = resultBuffer[currByte+1].ToString("x2");
                        part2 = (part1 + part2).TrimStart('0');
                        if (String.IsNullOrEmpty(part2))
                        {
                            part2 = "0";
                        }
                        ipadd.Append(part2);
                        currByte += 2;

                        i++;
                    }
                }
                else if (answerType == DnsType.CNAME)
                {
                    int endOfData = currByte + dataLength;
                    StringBuilder sb2 = new StringBuilder();
                    string cname;

                    int cnameLengthLeft = dataLength;

                    int startOfCname = currByte;
                    string cnameStr = ReadString(resultBuffer, ref currByte, dataLength);
                    this.pointerList[startOfCname]  = cnameStr;

                    sb2.Append(cnameStr);
                    ipadd = sb2;
                    cname = sb2.ToString();
                }
                else
                {
                    throw new Exception("Unacceptable DNS Type found in Answer");
                }
                   
                Console.WriteLine(String.Format(";{0,-20} {1,-10} {2, -10} {3, -20} {4, -20}",
                        resultStr, ttl, "IN", dnsTypeString, ipadd.ToString()));
            }  
        }

        private int TranslateBytes(byte[] sourceArr, int startIndex, int length)
        {
            byte[] targetBytes = new byte[length];

            Array.Copy(sourceArr, startIndex, targetBytes, 0, length);
            targetBytes = targetBytes.Reverse().ToArray();

            Int32 result;
            if (length == 4)
            {
                result = BitConverter.ToInt32(targetBytes);
                
            } else
            {
                result = BitConverter.ToInt16(targetBytes);
            }
            
            return result;
        }

        private void SendUdpRequest(Byte[] data, IPAddress dnsAddress, string hostname, DnsType dnsType)
        {
            try
            {
                var client = new UdpClient(5080);
                
                var ep = new IPEndPoint(dnsAddress, 53);


                Console.WriteLine();
                Console.WriteLine(";; QUESTION SECTION");
                string typeStr = (dnsType == DnsType.A) ? "A" : "AAAA";
                Console.WriteLine(String.Format(";{0,-31} {1,-10} {2, -10} \n",
                        hostname, "IN", typeStr));

                var watch = System.Diagnostics.Stopwatch.StartNew();            
                var i = client.Send(data, data.Length, ep);
                byte[] response = client.Receive(ref ep);
                watch.Stop();

                ReadResponse(response, hostname);

                Console.WriteLine();
                Console.WriteLine(";;Query Time: " + watch.ElapsedMilliseconds + "ms");
                Console.WriteLine(";;Server: " + dnsAddress);
                Console.WriteLine(";;WHEN: " + DateTime.Now.ToString("dddd MMM dd HH:mm:ss yyyy"));
                Console.WriteLine(";;MSG SIZE rcvd: " + response.Length);
                
            }
            catch (Exception e)
            {
                Console.WriteLine("UDP Client failed");
            }
        }

        private IPAddress FindDefaultDNS()
        {
            Boolean isValid;

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

            throw new Exception("Could not find default DNS Server");
        }

        private string ReadString(byte[] resultBuffer, ref int currByte, int maxBytesToRead)
        {
            StringBuilder sb = new StringBuilder();
            int bytesRead = 0;


            while (resultBuffer[currByte] != 0 && bytesRead < maxBytesToRead)
            {            
                // pointer detected!!!!!
                if (resultBuffer[currByte] == 0xc0)
                {
                    int offset = resultBuffer[currByte + 1];
                    string pointerVal = GetPointerValue(resultBuffer, offset);
                    sb.Append(pointerVal);

                    //ReadPointer(resultBuffer, currByte, sb);
                    currByte += 2;
                    bytesRead += 2;
                }
                else
                {                    
                    int runLengthLeft;

                    int startByte = currByte;
                    int pastStrLen = sb.Length;


                    int pointer = currByte; //= currByte + 2;
                    
                    runLengthLeft = resultBuffer[pointer];
                    pointer++;
                    bytesRead++;

                    while (runLengthLeft > 0)   //resultBuffer[pointer] != 0)
                    {
                        sb.Append(Encoding.ASCII.GetString(new[] { resultBuffer[pointer] }));
                        runLengthLeft--;
                        pointer++;
                        bytesRead++;
                    }

                    this.pointerList[startByte] = sb.ToString(pastStrLen, currByte - startByte);

                    if (resultBuffer[pointer] != 0) { sb.Append("."); }                    

                    currByte = pointer++;
                    
                }
            }


            return sb.ToString();
        }

        private string GetPointerValue(byte[] resultBuffer, int ptrStart)
        {
            int offset = 0; ;
            int nextPointerInd = ptrStart + offset;
            Boolean done = false;


            StringBuilder pointerValue = new StringBuilder();
            while (!done)
            {
                string currStr = pointerList[nextPointerInd];
                nextPointerInd = nextPointerInd + currStr.Length + 1;

                pointerValue.Append(currStr);
                

                if (!pointerList.ContainsKey(nextPointerInd))
                {
                    done = true;
                }
                else
                {
                    pointerValue.Append(".");
                }


            }

            return pointerValue.ToString();
        }

        private void ReadPointer(byte[] resultBuffer, int currByte, StringBuilder sb)
        {            


            int runLengthLeft; //= currByte + 1;

            int offset = resultBuffer[currByte + 1];
            int pointer = offset; //= currByte + 2;

            while (resultBuffer[pointer] != 0)
            {
                if (resultBuffer[pointer] == 0xc0)
                {
                    ReadPointer(resultBuffer, currByte, sb);
                    currByte += 2;
                }
                else
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
            }

            //resultStr = sb.ToString();            
        }

    }

    
}
