using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TestWinService
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            long[] rec = null;
            byte[] status = null;

            //retval :
            // Invalid User Pass=0,
            // Successfull = 1,
            // No Credit = 2,
            // DailyLimit = 3,
            // SendLimit = 4,
            // Invalid Number = 5
            // System IS Disable = 6
            // Bad Words= 7
            // Pardis Minimum Receivers=8
            // Number Is Public=9

            //Status :
            // Sent=0,
            // Failed=1

            MyWebService.Send sms = new MyWebService.Send();

            int retval = sms.SendSms("demo", "demo", txtRec.Text.Split(new char[] { ',' }), txtNum.Text, txtMsg.Text, false, "", ref rec, ref status);

            MessageBox.Show(retval.ToString());

            if (retval == 1)
            {
                for (int i = 0; i < status.Length; i++)
                {
                    MessageBox.Show(status[i].ToString() + "|" + rec[i].ToString());

                }
            }


        }
    }
}
