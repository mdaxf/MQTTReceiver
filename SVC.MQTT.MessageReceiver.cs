using System;
using System.Text;
using System.Xml;
using System.Net.Sockets;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Security.Cryptography.X509Certificates;
using System.Net;


namespace SVC.MessageReceiver
{
    public class SVCMQTTMessageReceiver
    {
        public string Mqttbroker;
        public string MqttbrokerAlias;
        public int MqttbrokerPort = 0;
        public string UserName;
        public string Password;
        public string Certificationfile;
        public string RootCertfile;
        public string ssl;
        MqttSslProtocols sslprotocol;
        public string ConnectionMethod;
        public string[] MqttTopicList;
        public string[] MqttTopicAliasList;
        public string RestWebServiceUrl;
        public string baseCallRestWebServiceUrl;
        public string RestApiKey;
        public string RestClientID;
        public string commRestApiKey;
        public string commRestClientID;
        public string commbaseCallRestWebServiceUrl;
        public string commRestWebServiceUrl;

        public  bool Disconnect;

        private MqttClient _client;

        private SVCLogger logger = new SVCLogger();
        public SVCMQTTMessageReceiver()
        {

        }

        public void Initialize()
        {
            if (RestWebServiceUrl == "" && commRestWebServiceUrl != "")
                RestWebServiceUrl = commRestWebServiceUrl;

            if (baseCallRestWebServiceUrl == "" && commbaseCallRestWebServiceUrl != "")
                baseCallRestWebServiceUrl = commbaseCallRestWebServiceUrl;

            if (RestApiKey == "" && commRestApiKey != "")
                RestApiKey = commRestApiKey;


            if (RestClientID == "" && commRestClientID != "")
                RestClientID = commRestClientID;

            switch (ssl)
            {
                case "":
                case "NONE":
                    sslprotocol = MqttSslProtocols.None;
                    break;
                case "SSL":
                    sslprotocol = MqttSslProtocols.SSLv3;
                    break;
                case "TLSV1.0":
                    sslprotocol = MqttSslProtocols.TLSv1_0;
                    break;
                case "TLSV1.1":
                    sslprotocol = MqttSslProtocols.TLSv1_1;
                    break;
                case "TLSV1.2":
                    sslprotocol = MqttSslProtocols.TLSv1_2;
                    break;
                default:
                    sslprotocol = MqttSslProtocols.None;
                    break;
            }
        }

        public void SVCMQTTConnect()
        {
            Initialize();
            var logstr = "Initialize SVCMQTTMessageReceiver:" + "Broker:" + Mqttbroker + ", "
                    + "MqttbrokerPort:" + MqttbrokerPort + ","
                    + "UserName:" + UserName + ","
                    + "Password:" + Password + ","
                    + "Certificationfile:" + Certificationfile + ","
                    + "RootCertfile:" + RootCertfile + ","
                    + "sslprotocol:" + sslprotocol + ","
                    + "ConnectionMethod:" + ConnectionMethod + ","
                    + "baseCallRestWebServiceUrl:" + baseCallRestWebServiceUrl + ","
                    + "RestWebServiceUrl:" + RestWebServiceUrl + ","
                    + "RestApiKey:" + RestApiKey;

            logger.DebugLoger(logstr);
            //Console.WriteLine(logstr);
            Disconnect = false;
            if (Mqttbroker == "")
            {
                //   Console.WriteLine(logstr);
                logger.InfoLoger("MQTT broker cannot be empty!");
                throw new Exception("MQTT broker cannot be empty!");

            }

            if (MqttTopicList.Length == 0)
            {
                logger.InfoLoger("MQTT Topic cannot be empty!");
                throw new Exception("MQTT Topic cannot be empty!");
            }
            IPAddress remoteIpAddress = null;
            try
            {
                remoteIpAddress = IPAddress.Parse(Mqttbroker);

            }
            catch { }

            if(remoteIpAddress == null)
            {
                try
                {
                    string localhost = Dns.GetHostName();
                    IPAddress[] IPAddressList = Dns.GetHostAddresses(Mqttbroker);
                    if (IPAddressList.Length > 0)
                    {
                        // check for the first address not null
                        // it seems that with .Net Micro Framework, the IPV6 addresses aren't supported and return "null"
                        int i = 0;
                        while (IPAddressList[i] == null) i++;
                        remoteIpAddress = IPAddressList[i];
                    }
                    else
                    {
                        throw new Exception("No address found for the remote host name");
                    }
                }
                catch(Exception ex)
                {
                    logger.InfoLoger("There is error during pharse Mqtt broke host name! " + ex.Message);
                    throw new Exception("There is error during pharse Mqtt broke host name! " + ex.Message);
                }

            }
            
            try
            {
                logger.DebugLoger("Start creating client to MQTT broker:" + Mqttbroker);
                
                // create client instance
                if (Certificationfile != "" && RootCertfile != "")
                {
                    // create a new X509Certificate instance from the certificate file
                    X509Certificate clientcert = new X509Certificate(Certificationfile);
                    var caCert = X509Certificate.CreateFromSignedFile(RootCertfile);
                    //  if (MqttbrokerPort != 0)
                    _client = new MqttClient(Mqttbroker, MqttbrokerPort, true, clientcert, caCert, sslprotocol);
                }
                else
                {
                    if (MqttbrokerPort != 0)
                        //_client = new MqttClient(Mqttbroker, MqttbrokerPort, false, null, null, MqttSslProtocols.None);
                        _client = new MqttClient(Mqttbroker, MqttbrokerPort, false, null, null, MqttSslProtocols.None);
                    else
                        _client = new MqttClient(Mqttbroker);
                }
                logger.DebugLoger("Create client to MQTT broker:" + Mqttbroker);
                // register to message received
                _client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

                //_client.MqttMsgDisconnected += client_MqttMsgDisconnected;

                // connect to the broker
                var _clientID = Guid.NewGuid().ToString();
                if (UserName != "")
                {
                    _client.Connect(_clientID, UserName, Password);
                }
                else
                {
                    _client.Connect(_clientID);
                }
                logger.DebugLoger("Connect to MQTT broker:" + Mqttbroker + " with clientid:" + _clientID);
                // subscribe to the topics
                byte[] qoslist = new byte[MqttTopicList.Length];
                int j = 0;
                foreach (string str in MqttTopicList)
                {
                    qoslist[j] = MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE;
                }
                _client.Subscribe(MqttTopicList, qoslist);
            }
            catch (Exception ex)
            {
                logger.DebugLoger("There is error during connecting to Mqtt broke! " + ex.Message);
                throw new Exception("There is error during connecting to Mqtt broke! " + ex.Message);
            }
        }

        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            // handle message received
            Console.WriteLine("Received from topic: " + e.Topic  + " Message:"+Encoding.UTF8.GetString(e.Message));

            
            string deviceData = Encoding.UTF8.GetString(e.Message);
            logger.DebugLoger("Received the Message:" + "Broker:" + Mqttbroker + ", "
                    +"message: " + deviceData
              );
            SVCProcessData ProcessData = new SVCProcessData();

            ProcessData.baseCallRestWebServiceUrl = baseCallRestWebServiceUrl;
            ProcessData.RestApiKey = RestApiKey;
            ProcessData.RestClientID = RestClientID;
            ProcessData.Mqttbroker = Mqttbroker;
            ProcessData.MqttbrokerAlias = MqttbrokerAlias;
            ProcessData.RestWebServiceUrl = RestWebServiceUrl;
            

            for(int i=0; i<MqttTopicList.Length;i++)
            {
                if(MqttTopicList[i] == e.Topic)
                {
                    ProcessData.TopicAlias = MqttTopicAliasList[i];
                    break;
                }
            }
            ProcessData.ProcessDeviceData(e.Topic,deviceData);
        }
        void client_MqttMsgDisconnected(object sender, EventArgs e)
        {
           // Console.WriteLine("Disconnected from MQTT broker.");
            logger.DebugLoger("Disconnected from MQTT broker." + "Broker:" + Mqttbroker
              );
            
            if (!Disconnect) { 
                // Reconnect to the MQTT broker
                MqttClient client = (MqttClient)sender;
                client.Connect(client.ClientId);
                client.Subscribe(MqttTopicList, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            }
        }
       
    }
    class SVCProcessData
    {
        public string baseCallRestWebServiceUrl;
        public string RestApiKey;
        public string RestClientID;
        public string Mqttbroker;
        public string MqttbrokerAlias;
        public string RestWebServiceUrl;
        public string TopicAlias;
        public SVCProcessData()
        {

        }
        public void ProcessDeviceData(string topic, string deviceData)
        {
            string CallRestWebServiceUrl = RestWebServiceUrl;


            if (CallRestWebServiceUrl != "")
            {
                SVCHttpHelper.apiBasicUri = baseCallRestWebServiceUrl;
                SVCHttpHelper.ApiKey = RestApiKey;
                SVCHttpHelper.clientid = RestClientID;
                SVCHttpHelper.broke = Mqttbroker;
                SVCHttpHelper.brokealias = MqttbrokerAlias;
                SVCHttpHelper.topic = topic;
                SVCHttpHelper.topicalias = TopicAlias;
                SVCHttpHelper.Post(CallRestWebServiceUrl, deviceData);

            }

        }

    }
    
}
