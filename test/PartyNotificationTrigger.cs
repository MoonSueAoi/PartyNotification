using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Advanced_Combat_Tracker;

namespace FF14PartyNotification
{
    public class PartyNotificationTrigger : IActPluginV1
    {
        private bool notificationSent = false;
        private TextBox keyInputBox;
        private TextBox uidInputBox; 
        private CheckBox useUidCheckBox;
        private Button saveButton;
        private Button testButton;
        private Label statusLabel;
        private string sendKey;
        private const string CONFIG_FILE = "PartyNotificationConfig.xml";
        private const string PARTY_COMPLETE_MESSAGE = "招募队员结束，队员已经集齐。";
        private static readonly Regex PARTY_COMPLETE_REGEX = new Regex(@"^.{14} ChatLog 00:0039::.招募队员结束，队员已经集齐。");
        private static readonly Regex Matcha_Alert_REGEX = new Regex(@"^.{14} ChatLog 00:0:Matcha#25.2.18.1428#chs-MatchAlert");
        private HttpClient client = new HttpClient();

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            pluginScreenSpace.Controls.Add(CreateConfigUI());
            LoadConfig();
            ActGlobals.oFormActMain.OnLogLineRead += new LogLineEventDelegate(OnLogLineRead);
            pluginStatusText.Text = "Plugin Loaded";
        }


        private Control CreateConfigUI()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var label = new Label
            {
                Text = "Server酱Key:",
                Location = new System.Drawing.Point(10, 15),
                AutoSize = true
            };
            panel.Controls.Add(label);

            keyInputBox = new TextBox
            {
                Location = new System.Drawing.Point(120, 12),
                Width = 300,
                Text = sendKey
            };
            panel.Controls.Add(keyInputBox);

            var uidLabel = new Label
            {
                Text = "UID:",
                Location = new System.Drawing.Point(10, 45),
                AutoSize = true
            };
            panel.Controls.Add(uidLabel);

            uidInputBox = new TextBox
            {
                Location = new System.Drawing.Point(120, 42),
                Width = 300,
                Enabled = false // 初始状态设置为禁用
            };
            panel.Controls.Add(uidInputBox);

            useUidCheckBox = new CheckBox
            {
                Text = "使用server酱3推送则勾选",
                Location = new System.Drawing.Point(10, 70),
                AutoSize = true
            };
            useUidCheckBox.CheckedChanged += UseUidCheckBox_CheckedChanged; // 添加事件处理器
            panel.Controls.Add(useUidCheckBox);

            saveButton = new Button
            {
                Text = "保存配置",
                Location = new System.Drawing.Point(120, 100),
                Width = 100,
                Height = 50
            };
            saveButton.Click += SaveButton_Click;
            panel.Controls.Add(saveButton);

            testButton = new Button
            {
                Text = "测试通知",
                Location = new System.Drawing.Point(230, 100),
                Width = 100,
                Height = 50
            };
            testButton.Click += TestButton_Click;
            panel.Controls.Add(testButton);

            statusLabel = new Label
            {
                Location = new System.Drawing.Point(330, 110),
                AutoSize = true
            };
            panel.Controls.Add(statusLabel);

            LoadConfig();

            return panel;
        }

        private void UseUidCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // 根据useUidCheckBox的选中状态来启用或禁用uidInputBox
            uidInputBox.Enabled = useUidCheckBox.Checked;
        }

        private async void SaveButton_Click(object sender, EventArgs e)
        {
            sendKey = keyInputBox.Text;
            SaveConfig();
            statusLabel.Text = "配置已保存";
        }

        private async void TestButton_Click(object sender, EventArgs e)
        {
            Console.Write("true");
            string testTitle = "测试通知";
            string testContent = "这是一个测试通知";
            await SendNotification(testTitle, testContent);
        }

        private async Task SendNotification(string title, string content)
        {
            Console.Write("true");
            try
            {
                var values = new Dictionary<string, string>
                {
                    { "title", title },
                    { "desp", content }
                };

                var postContent = new FormUrlEncodedContent(values);
                string url;

                // 根据是否勾选使用UID来决定URL
                if (useUidCheckBox.Checked && !string.IsNullOrEmpty(uidInputBox.Text))
                {
                    url = $"https://{uidInputBox.Text}.push.ft07.com/send/{sendKey}.send";
                }
                else
                {
                    url = $"https://sctapi.ftqq.com/{sendKey}.send";
                }

                var response = await client.PostAsync(url, postContent);

                if (!response.IsSuccessStatusCode)
                {
                    ActGlobals.oFormActMain.WriteExceptionLog(
                        new Exception($"Notification failed: {response.StatusCode}"),
                        "PartyNotificationTrigger"
                    );
                    statusLabel.Text = "通知发送失败";
                }
                else
                {
                    statusLabel.Text = "通知发送成功";
                }
            }
            catch (Exception ex)
            {
                ActGlobals.oFormActMain.WriteExceptionLog(ex, "SendNotification Error");
                statusLabel.Text = "通知发送异常";
            }
        }

        private void LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, CONFIG_FILE);
                if (File.Exists(configPath))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(configPath);
                    sendKey = doc.SelectSingleNode("//Config/SendKey")?.InnerText ?? "";
                    string useUid = doc.SelectSingleNode("//Config/UseUid")?.InnerText ?? "false";
                    bool.TryParse(useUid, out bool useUidValue);
                    useUidCheckBox.Checked = useUidValue; // 设置勾选状态
                    uidInputBox.Text = doc.SelectSingleNode("//Config/Uid")?.InnerText ?? "";

                    if (keyInputBox != null)
                        keyInputBox.Text = sendKey;

                    // 根据勾选状态启用或禁用uidInputBox
                    uidInputBox.Enabled = useUidValue;
                }
            }
            catch (Exception ex)
            {
                if (statusLabel != null)
                    statusLabel.Text = "加载配置失败：" + ex.Message;
            }
        }

        private void SaveConfig()
        {
            try
            {
                string configPath = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, CONFIG_FILE);
                XmlDocument doc = new XmlDocument();
                XmlElement root = doc.CreateElement("Config");
                doc.AppendChild(root);

                XmlElement keyElement = doc.CreateElement("SendKey");
                keyElement.InnerText = sendKey;
                root.AppendChild(keyElement);

                XmlElement useUidElement = doc.CreateElement("UseUid"); // 新增
                useUidElement.InnerText = useUidCheckBox.Checked.ToString();
                root.AppendChild(useUidElement);

                XmlElement uidElement = doc.CreateElement("Uid"); // 新增，用于保存UID值
                uidElement.InnerText = uidInputBox.Text;
                root.AppendChild(uidElement);

                doc.Save(configPath);
            }
            catch (Exception ex)
            {
                if (statusLabel != null)
                    statusLabel.Text = "保存配置失败：" + ex.Message;
            }
        }
        public void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            statusLabel.Text = "通知发送成功";
            if (string.IsNullOrEmpty(sendKey)) return;
            try
            {
                // 检查是否匹配招募完成信息
                if (PARTY_COMPLETE_REGEX.IsMatch(logInfo.logLine) && !notificationSent)
                {
                    Console.WriteLine("true");
                    SendNotification("FF14招募完成", "招募队员已集齐，可以开始活动了！");
                    notificationSent = true;
                    statusLabel.Text = "通知发送成功";
                    // 30秒后重置通知状态
                    Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
                    {
                        notificationSent = false;
                    });
                }

                if (Matcha_Alert_REGEX.IsMatch(logInfo.logLine) && !notificationSent)
                {
                    Console.WriteLine("true");
                    SendNotification("FF14招募完成", "招募队员已集齐，可以开始活动了！");
                    notificationSent = true;
                    statusLabel.Text = "通知发送成功";
                    // 30秒后重置通知状态
                    Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
                    {
                        notificationSent = false;
                    });
                }
            }
            catch (Exception ex)
            {
                ActGlobals.oFormActMain.WriteExceptionLog(ex, "Log parsing error");
            }
        }

        

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.OnLogLineRead -= new LogLineEventDelegate(OnLogLineRead);
        }
    
    #region IActPluginV1 Members

    public string PluginName => "FF14 Party Notification";

        public string PluginVersion => "1.0";

        public string PluginAuthor => "mukusyuu";

        public string PluginDescription => "Notifies when an FF14 party is formed.";

        

        #endregion
    }
}
