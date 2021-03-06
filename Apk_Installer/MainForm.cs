﻿using AndroidCtrl;
using AndroidCtrl.ADB;
using AndroidCtrl.Tools;
using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Apk_Installer
{
    public partial class MainForm : Form
    {
        private static string LoadedApk = null;
        private static string ApkName = null;
        private static string DeviceName = null;
        private static int ApkSdk = 0;
        private static int DeviceSdk = 0;
        private Config config;

        public MainForm(string arg)
        {
            InitializeComponent();

            this.Text = Application.ProductName + " v" + Application.ProductVersion;

            config = new Config();
            textIP.Text = config.getIPaddress();
            textPort.Text = config.getPort().ToString();
            if (config.getCheckExt())
            {
                bool setAssoc = FileAssociation.SetAssociation();
                if (config.getCheckExt() != setAssoc)
                    config.setCheckExt(setAssoc);
            }

            groupBox1.AllowDrop = true;
            pictureBox1.Image = Properties.Resources.apk.ToBitmap();

            InitializeAdb();
            InitializeApk(arg);
            ScanDevice();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            toolTip1.SetToolTip(textIP, "Input target ip address of adb wireless");
            toolTip1.SetToolTip(textPort, "Input target port of adb wireless. default is 5555");
            toolTip1.SetToolTip(groupBox1, "You can drag and drop apk file in here, and see what happen");
            toolTip1.SetToolTip(comboBox1, "Select available device what you want");
            toolTip1.SetToolTip(btnScan, "Scan connected device");
            toolTip1.SetToolTip(btnConnect, "Connect to adb wireless");
            toolTip1.SetToolTip(btnInstall, "Install loaded apk into device");
        }

        private void InitializeAdb()
        {
            try
            {
                if (!File.Exists("adb/aapt.exe"))
                    Deploy.AAPT();
                if (!File.Exists("adb/adb.exe") ||
                    !File.Exists("adb/AdbWinApi.dll") ||
                    !File.Exists("adb/AdbWinUsbApi.dll"))
                    Deploy.ADB();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            if (!ADB.IsStarted)
                ADB.Start();
        }

        private void InitializeApk(string fileApk = null)
        {
            if (File.Exists(fileApk) && fileApk.ToLower().EndsWith(".apk"))
            {
                try
                {
                    ApkFile Apk = new ApkFile(fileApk);
                    if (Apk.isApk())
                    {
                        labelPackage.Text = setLabel(Apk.getPackageName());
                        labelName.Text = setLabel(Apk.getAppLabel());
                        labelVersion.Text = setLabel(Apk.getVersion());
                        labelSdk.Text = setLabel(Apk.getSdkVersion());
                        pictureBox1.Image = setIcon(Apk.getIcon());
                        btnInstall.Enabled = (comboBox1.Items.Count > 0) && Apk.isApk();
                        LoadedApk = fileApk;

                        ApkName = $"{Apk.getAppLabel()} v{Apk.getVersion()}";
                        int.TryParse(Apk.getSdkVersion(), out ApkSdk);
                    }
                    else
                    {
                        ClearApkInfo();
                        MessageBox.Show($"File {Path.GetFileName(fileApk)} is not APK!", "Something went wrong",
                            MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    }
                }
                catch (Exception e)
                {
                    ClearApkInfo();
                    MessageBox.Show($"Can't open file {Path.GetFileName(fileApk)}!\nReason: {e.Message}", "Something went wrong",
                        MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
            else
            {
                ClearApkInfo();
            }
        }

        private void ClearApkInfo()
        {
            labelPackage.Text = setLabel();
            labelName.Text = setLabel();
            labelVersion.Text = setLabel();
            labelSdk.Text = setLabel();
            pictureBox1.Image = setIcon();
            btnInstall.Enabled = false;
            LoadedApk = null;

            ApkName = String.Empty;
            ApkSdk = 0;
        }

        private void ScanDevice(bool set = false)
        {
            if (!ADB.IsStarted) ADB.Start();

            int num = 0;
            comboBox1_Clear();

            foreach (DataModelDevicesItem device in ADB.Devices())
            {
                if (device.Device != null)
                {
                    ADB.SelectDevice(device);
                    ComboboxItem item = new ComboboxItem();
                    item.DataModel = device;
                    item.Id = device.Serial;
                    string brand = ADB.Instance().Device.BuildProperties.Get("ro.product.brand");
                    item.Brand = UpperCaseFirst(brand);
                    string model = ADB.Instance().Device.BuildProperties.Get("ro.product.model");
                    item.Model = UpperCaseFirst(model);
                    string codename = ADB.Instance().Device.BuildProperties.Get("ro.product.device");
                    item.CodeName = UpperCaseFirst(codename);
                    int index = comboBox1.Items.Add(item);
                    if (device.Serial.StartsWith(textIP.Text))
                    {
                        if (set) num = index;
                    }
                }
            }

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = num;
                btnInstall.Enabled = LoadedApk != null;
            }
        }

        private void comboBox1_Clear()
        {
            comboBox1.Items.Clear();
            labelDevice.Text = setLabel();
            labelAndroid.Text = setLabel();
            labelDeviceSdk.Text = setLabel();
            labelSerial.Text = setLabel();
            btnInstall.Enabled = false;

            DeviceName = string.Empty;
            DeviceSdk = 0;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var data = (comboBox1.SelectedItem as ComboboxItem);
            ADB.SelectDevice(data.DataModel);
            labelDevice.Text = setLabel(data.ToString());
            string android = ADB.Instance().Device.BuildProperties.Get("ro.build.version.release");
            labelAndroid.Text = setLabel(android);
            string sdk = ADB.Instance().Device.BuildProperties.Get("ro.build.version.sdk");
            labelDeviceSdk.Text = setLabel(sdk);
            labelSerial.Text = setLabel(data.Id);

            DeviceName = data.ToString();
            int.TryParse(sdk, out DeviceSdk);
        }

        private static string UpperCaseFirst(string text)
        {
            try
            {
                string result = null;
                string[] words = text.Split(' ');
                foreach (string word in words)
                {
                    result += char.ToUpper(word[0]) + word.Substring(1) + " ";
                }
                return result.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error : " + ex.Message);
                return text;
            }
        }

        private string setLabel(string text = null)
        {
            return text != null ? $": {text}" : ": ...";
        }

        private Image setIcon(Stream img = null)
        {
            if (img == null)
                return Properties.Resources.apk.ToBitmap();
            else
                return Image.FromStream(img);
        }

        private void setBusy(int groupbox, bool value)
        {
            groupBox1.AllowDrop = !value;
            groupBox1.Text = !value ? "1. APK File ( Drag && Drop Here )" : "1. APK File";

            groupBox2.Enabled = !value;
            groupBox3.Enabled = !value;

            if (groupbox == 1)
            {
                groupBox2.Text = !value ? "2. ADB Devices" : "2. Connecting to Devices ( please wait )";
            }
            else if (groupbox == 2)
            {
                groupBox2.Text = !value ? "2. ADB Devices" : "2. Scanning Devices ( please wait )";
            }
            else if (groupbox == 3)
            {
                groupBox3.Text = !value ? "3. Connected Device" : "3. Installing to Device ( please wait )";
            }

            btnInstall.Enabled = !value && (LoadedApk != null) && (comboBox1.Items.Count > 0);
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            setBusy(1, true);

            if (!ADB.IsStarted) ADB.Start();
            await Task.Run(() => ADB.Connect(textIP.Text, textPort.Text));

            config.setAdbWifi(textIP.Text, int.Parse(textPort.Text));

            setBusy(2, true);
            ScanDevice(true);

            setBusy(2, false);
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            setBusy(2, true);
            ScanDevice();

            setBusy(2, false);
        }

        private async void btnInstall_Click(object sender, EventArgs e)
        {
            if (LoadedApk != null)
            {
                if (ApkSdk <= DeviceSdk)
                {
                    if (ADB.IsStarted)
                    {
                        setBusy(3, true);
                        await Task.Run(() =>
                        {
                            if (ADB.Instance().Install(LoadedApk, "-r"))
                            {
                                MessageBox.Show($"Successfully install {ApkName} on {DeviceName}!", ApkName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                                InitializeApk();
                            }
                            else
                            {
                                MessageBox.Show($"Something went wrong!!!\nCan't install those apk into {DeviceName}.", ApkName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        });

                        setBusy(3, false);
                    }
                    else
                    {
                        MessageBox.Show("ADB is not running!!! Solve: step no.2", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show($"This apk require a sdk min {ApkSdk}, and your device is {DeviceSdk}.", "Device is not compatible!",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("There is no APK file!!! Solve: step no.1", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void groupBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void groupBox1_DragDrop(object sender, DragEventArgs e)
        {
            string[] FileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            foreach (string Content in FileList)
            {
                if (Content.ToLower().EndsWith(".apk"))
                {
                    InitializeApk(Content);
                    break;
                }
            }
        }

        private void txtNumeric_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !(char.IsDigit(e.KeyChar) || (e.KeyChar == '.') ||
                (e.KeyChar == (char)Keys.Back) || (e.KeyChar == (char)Keys.Delete));
        }

        private bool onValidation = false;

        private void txtValidation(object sender, EventArgs e)
        {
            if (!onValidation)
            {
                onValidation = true;
                Regex regex = new Regex(@"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
                Match match = regex.Match(textIP.Text);
                btnConnect.Enabled = match.Success;
                if (match.Success)
                    textIP.Text = match.Value;

                textPort.Text = textPort.Text.Replace(".", "");
                onValidation = false;
            }
        }
    }
}