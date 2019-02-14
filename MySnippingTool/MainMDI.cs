﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MySnippingTool
{
    public partial class MainMDI : Form
    {
        [DllImport("user32.dll", SetLastError=true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError=true)]
        private static extern bool CloseClipboard();

        private int errorCount = 0;
        private string snippingToolFilePath = "";
        public MainMDI()
        {
            InitializeComponent();
            if (!Directory.Exists("Temporary Images"))
                Directory.CreateDirectory("Temporary Images");
            else
            {
                DirectoryInfo dirInfo = new DirectoryInfo("Temporary Images");
                long size = 0;
                // Add file sizes.
                FileInfo[] fis = dirInfo.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    size += fi.Length;
                }

                if (size > 100000000) //100 MB
                {
                    foreach (FileInfo fi in fis)
                    {
                        fi.Delete();
                    }
                }
                
            }

            if (File.Exists(@"C:\windows\sysnative\SnippingTool.exe"))
                snippingToolFilePath = @"C:\windows\sysnative\SnippingTool.exe";
            else if (File.Exists(@"C:\windows\system32\SnippingTool.exe"))
                snippingToolFilePath = @"C:\windows\system32\SnippingTool.exe";
            else
            {
                MessageBox.Show("SnippingTool does not exist in the windows, please install it first. Application is closing...");
                this.Close();
            }

            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;
            if (settings["Directory"] != null)
                toolStripStatusLabel.Text = settings["Directory"].Value == "" ? "Please select folder for the storing images" : $" Selected Folder : {settings["Directory"].Value}";


        }

        private void ShowNewForm(object sender, EventArgs e)
        {
            FormWindowState mainState = this.WindowState;

            //cleaning clipboard
            IntPtr hwnd = new IntPtr();

            PleaseWait waitForm = new PleaseWait();

            while (true)
            {
                if (OpenClipboard(hwnd))
                {
                    EmptyClipboard();
                    CloseClipboard();
                    break;
                }

                errorCount = errorCount + 1;

                if (errorCount == 1)
                {
                    waitForm.MdiParent = this;
                    waitForm.Show();
                    waitForm.Focus();
                    waitForm.BringToFront();
                    waitForm.TopMost = true;
                    this.Refresh();
                }
            }

            waitForm.Close();
            waitForm.Dispose();
            this.Refresh();


            try
            {
                using (Process snippingToolProcess = new Process())
                {                   
                    // Get the process start information of notepad.
                    ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(snippingToolFilePath, "/clip");

                    // Assign 'StartInfo' of notepad to 'StartInfo' of 'Process' object.
                    snippingToolProcess.StartInfo = myProcessStartInfo;


                    this.WindowState = FormWindowState.Minimized;

                    // Create a SnippingTool.
                    snippingToolProcess.Start();
                    

                    //save clipboard to a file in temporary folder
                    //Process can not capture exit message of the snipping tool so "while" loop saves us 
                    while (true)
                    {
                        if (Clipboard.ContainsImage())
                        {
                            string fileName = $"Temporary Images\\{Guid.NewGuid().ToString()}.jpg";

                            Clipboard.GetImage().Save(fileName, ImageFormat.Jpeg);

                            //Close it
                            snippingToolProcess.Close();

                            // 
                            // pictureBox
                            // 
                            CustomPictureBox pictureBox = new CustomPictureBox();
                            pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
                            pictureBox.Location = new System.Drawing.Point(0, 0);
                            pictureBox.Name = $"pictureBox_{DateTime.Now.ToString("yyyyMMddmmhhfff")}";
                            pictureBox.TabIndex = 0;
                            pictureBox.TabStop = false;
                            pictureBox.Image = Image.FromFile(fileName);
                            pictureBox.ClientSize = pictureBox.Image.Size;
                            pictureBox.Paint += PictureBox_Paint;
                            pictureBox.isItSaved = false;

                            // 
                            // Form
                            // 
                            Form form = new Form();
                            form.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
                            form.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                            form.ClientSize = pictureBox.ClientSize;
                            form.Text = "*";
                            form.Name = $"Form_{DateTime.Now.ToString("yyyyMMddmmhhfff")} * ";
                            form.Controls.Add(pictureBox);
                            form.ResumeLayout(false);
                            form.Icon = this.Icon;
                            form.MaximizeBox = false;
                            form.MinimizeBox = false;
                            form.ControlBox = false;
                            form.FormBorderStyle = FormBorderStyle.FixedDialog;
                            form.KeyDown += Form_KeyDown;

                            form.MdiParent = this;

                            form.Show();

                            this.ActiveControl = form;                           

                            errorCount = 0;

                            this.WindowState = mainState;
                            
                            //weird workaround
                            this.TopMost = true;
                            //it locks other forms to be front later on so
                            this.TopMost = false;
                            //because
                            //this.TopLevel = true;
                            //does not work.

                            if (form.ClientSize.Width <= 131)
                                MessageBox.Show("very small object is drawn. Image will be adjusted for the window but original size will be kept in case of recording", "Warning", MessageBoxButtons.OK);

                            return;
                        }   
                    }

                }
            }
            catch
            {
                this.WindowState = mainState;
                this.BringToFront();
                this.TopMost = true;

                MessageBox.Show(this, "Error is occured... Please try again !");
            }

        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Modifiers == Keys.Control)
            {
                Clipboard.SetImage(((PictureBox)((Form)sender).Controls[0]).Image);
            }

            if (e.KeyCode == Keys.S && e.Modifiers == Keys.Control)
            {
                SaveToolStripMenuItem_Click(sender, e);
            }
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (((CustomPictureBox)sender).isItSaved)
                ControlPaint.DrawBorder(e.Graphics, ((CustomPictureBox)sender).ClientRectangle, Color.Green, ButtonBorderStyle.Solid);
            else
                ControlPaint.DrawBorder(e.Graphics, ((CustomPictureBox)sender).ClientRectangle, Color.Red, ButtonBorderStyle.Solid);
        }

        private void OpenFile(object sender, EventArgs e)
        {
            FolderBrowserDialog openFolderDialog = new FolderBrowserDialog();

            openFolderDialog.Description = "Select folder path ";
            openFolderDialog.SelectedPath = ConfigurationManager.AppSettings["Directory"].ToString();
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings["Directory"] == null)
                {
                    settings.Add("Directory", openFolderDialog.SelectedPath);
                }
                else
                {
                    settings["Directory"].Value = openFolderDialog.SelectedPath;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                toolStripStatusLabel.Text = $" Selected Folder : {settings["Directory"].Value}";
            }
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;
            if (settings["Directory"] == null)
            {
                MessageBox.Show("You should select path from folder selection in the toolbar");
                return;
            }
            else
            {
                if (!Directory.Exists(settings["Directory"].Value))
                {
                    MessageBox.Show("You should select path from folder selection in the toolbar");
                    return;
                }
                else
                {
                    // Determine the active child form.  
                    Form activeChild = this.ActiveMdiChild;

                    // If there is an active child form, find the active control    
                    if (activeChild != null)
                    {
                        CustomPictureBox pictureBox = (CustomPictureBox)activeChild.Controls[0];
                        if (pictureBox.isItSaved)
                        {
                            MessageBox.Show("It is already saved !");
                            return;
                        }
                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.AddExtension = true;
                        saveFileDialog.InitialDirectory = settings["Directory"].Value;
                        saveFileDialog.AddExtension = true;
                        saveFileDialog.CheckPathExists = true;
                        saveFileDialog.CheckFileExists = false;
                        saveFileDialog.Filter = "JPeg Image|*.jpg";
                        saveFileDialog.DefaultExt = "jpg";
                        saveFileDialog.Title = "Save an Image File...";
                        saveFileDialog.FileName = $"Img_{ DateTime.Now.ToString("yyyyMMddmmhhssfff")}.jpg";
                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {                           
                            pictureBox.Image.Save(saveFileDialog.FileName);
                            pictureBox.isItSaved = true;
                            pictureBox.Refresh();
                            activeChild.Text = activeChild.Name = Path.GetFileName(saveFileDialog.FileName);
                        }
                    }
                    else
                        MessageBox.Show("Select form to save...");
                }
            }
        }

        private void ExitToolsStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

       
        private void toolStripAboutButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Created by Muhammet ATALAY - http://www.muhammetatalay.com");
        }

        private void MainMDI_FormClosing(object sender, FormClosingEventArgs e)
        {
            bool hasUnsavedForm = false;

            foreach(Form form in this.MdiChildren)
            {
                hasUnsavedForm = form.Name.Contains("*") ? true : false;
            }

            if (hasUnsavedForm)
            {
                var window = MessageBox.Show(
                    "Close the window?",
                    "Are you sure?",
                    MessageBoxButtons.YesNo);

                e.Cancel = (window == DialogResult.No);
            }
        }

        private void SaveAlltoolStripButton_Click(object sender, EventArgs e)
        {
            foreach(Form form in this.MdiChildren)
            {
                this.ActiveControl = form;
                SaveToolStripMenuItem_Click(form, null);
            }

            MessageBox.Show(this.MdiChildren.Count().ToString() + " images created to the selected path successfully","Notification");
        }

        private void toolStripButtonClose_Click(object sender, EventArgs e)
        {
            if (this.ActiveControl is Form)
            {
                if (this.ActiveControl.Name.Contains("*"))
                {
                    var window = MessageBox.Show(
                       "Close the window?",
                       "Are you sure?",
                       MessageBoxButtons.YesNo);
                    if (window == DialogResult.No)
                        return;
                }

                ((Form)this.ActiveControl).Close();
            }
            else
            {
                MessageBox.Show("Select a form to close.");
            }
            
        }

        private void toolStripButtonCloseAll_Click(object sender, EventArgs e)
        {
            foreach (Form form in this.MdiChildren)
            {
                this.ActiveControl = form;

                toolStripButtonClose_Click(null, null);
            }
        }
    }
}
