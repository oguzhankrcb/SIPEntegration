using Serilog;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.SoftPhone;
using SIPSorcery.Sys;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.TaskScheduler;
using System.IO;

namespace SIPEntegration
{
    public partial class Form1 : Form
    {
        private const int SIP_CLIENT_COUNT = 2;                             // The number of SIP clients (simultaneous calls) that the UI can handle.
        private const int REGISTRATION_EXPIRY = 180;


        private string m_sipUsername = SIPSoftPhoneState.SIPUsername;
        private string m_sipPassword = SIPSoftPhoneState.SIPPassword;
        private string m_sipServer = SIPSoftPhoneState.SIPServer;

        private SIPTransportManager _sipTransportManager;
        private List<SIPClient> _sipClients;                 // STUN client to periodically check the public IP address.
        private SIPRegistrationUserAgent _sipRegistrationClient;    // Can be used to register with an external SIP provider if incoming calls are required.


        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            if (File.Exists(Environment.CurrentDirectory + "\\netgsm.txt"))
            {
                string[] infos = File.ReadAllLines(Application.StartupPath + "\\netgsm.txt");

                m_sipUsername = infos[0];
                m_sipPassword = infos[1];
                m_sipServer = infos[2];
            }
            else
            {
                MessageBox.Show("netgsm.txt bulunamadığı için ayarlar etkinleştirilemedi!", "HATA!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            //using (TaskService ts = new TaskService())
            //{
            //    if (ts.GetTask(@"Mikale SIP Entegration") == null)
            //    {
            //        TaskDefinition td = ts.NewTask();
            //        td.Settings.DisallowStartIfOnBatteries = false;
            //        td.Settings.StopIfGoingOnBatteries = false;
            //        td.Principal.RunLevel = TaskRunLevel.Highest;
            //        td.Settings.Compatibility = TaskCompatibility.V2_1;
            //        // td.
            //        td.RegistrationInfo.Description = "Mikale SIP Entegration";

            //        LogonTrigger logonTrigger = new LogonTrigger();
            //        logonTrigger.Enabled = true;
            //        logonTrigger.UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            //        td.Triggers.Add(logonTrigger);
            //        // Create an action that will launch Notepad whenever the trigger fires
            //        td.Actions.Add(new ExecAction(Assembly.GetExecutingAssembly().Location, null, null));
            //        // Register the task in the root folder
            //        ts.RootFolder.RegisterTaskDefinition(@"Mikale SIP Entegration", td);
            //    }
            //    else
            //    {
            //        Microsoft.Win32.TaskScheduler.Task task = ts.GetTask(@"Mikale SIP Entegration");
            //        ExecAction exec = (ExecAction)task.Definition.Actions[0];

            //        if (Assembly.GetExecutingAssembly().Location != exec.Path && ts.GetTask(@"Mikale SIP Entegration2") == null)
            //        {
            //            TaskDefinition td = ts.NewTask();
            //            td.Settings.DisallowStartIfOnBatteries = false;
            //            td.Settings.StopIfGoingOnBatteries = false;
            //            td.Principal.RunLevel = TaskRunLevel.Highest;
            //            td.Settings.Compatibility = TaskCompatibility.V2_1;
            //            // td.
            //            td.RegistrationInfo.Description = "Mikale SIP Entegration2";

            //            LogonTrigger logonTrigger = new LogonTrigger();
            //            logonTrigger.Enabled = true;
            //            logonTrigger.UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            //            td.Triggers.Add(logonTrigger);
            //            // Create an action that will launch Notepad whenever the trigger fires
            //            td.Actions.Add(new ExecAction(Assembly.GetExecutingAssembly().Location, null, null));
            //            // Register the task in the root folder
            //            ts.RootFolder.RegisterTaskDefinition(@"Mikale SIP Entegration2", td);
            //        }


            //    }
            //}


            ResetToCallStartState(null);

            _sipTransportManager = new SIPTransportManager();
            _sipTransportManager.IncomingCall += SIPCallIncoming;

            _sipClients = new List<SIPClient>();


            System.Threading.Tasks.Task.Run(Initialize).Wait();

            new Thread(() => 
            {
                Thread.Sleep(2000);
                this.Invoke(new MethodInvoker(() => { this.Visible = false; }));
            }
            ).Start();
        }


        private void ResetToCallStartState(SIPClient sipClient)
        {
            if (sipClient == null || sipClient == _sipClients[0])
            {
                
            }

            if (sipClient == null || sipClient == _sipClients[1])
            {
               
            }
        }

        private bool SIPCallIncoming(SIPRequest sipRequest)
        {
            byte[] data = Encoding.ASCII.GetBytes(sipRequest.Header.From.FriendlyDescription().Split(' ')[0]);

            File.AppendAllText("calls.log", "tel:" + "\"" + sipRequest.Header.From.FriendlyDescription().Split(' ')[0] + "\",tarih:\""  + DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "\"\r\n");

            try
            {
                using (UdpClient c = new UdpClient())
                {
                    c.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6060));
                    c.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", ex.ToString());
            }

            if (!_sipClients[0].IsCallActive)
            {
                _sipClients[0].Accept(sipRequest);


                return true;
            }
            else if (!_sipClients[1].IsCallActive)
            {
                _sipClients[1].Accept(sipRequest);

             

                return true;
            }
            else
            {
                return false;
            }
        }

        private async System.Threading.Tasks.Task Initialize()
        {
            await _sipTransportManager.InitialiseSIP2();

            for (int i = 0; i < SIP_CLIENT_COUNT; i++)
            {
                var sipClient = new SIPClient(_sipTransportManager.SIPTransport);


                sipClient.CallEnded += ResetToCallStartState;

                _sipClients.Add(sipClient);
            }

            string listeningEndPoints = null;

            foreach (var sipChannel in _sipTransportManager.SIPTransport.GetSIPChannels())
            {
                SIPEndPoint sipChannelEP = sipChannel.ListeningSIPEndPoint.CopyOf();
                sipChannelEP.ChannelID = null;
                listeningEndPoints += (listeningEndPoints == null) ? sipChannelEP.ToString() : $", {sipChannelEP}";
            }

            _sipRegistrationClient = new SIPRegistrationUserAgent(
                _sipTransportManager.SIPTransport,
                m_sipUsername,
                m_sipPassword,
                m_sipServer,
                REGISTRATION_EXPIRY);

            _sipRegistrationClient.Start();
        }

    }
}
