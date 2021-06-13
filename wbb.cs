/*********************************************************************************
WiiBalanceScale
MIT License
Copyright (c) 2021 Milo
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
**********************************************************************************/
using System.Windows.Forms;
using WiimoteLib;
using System.Collections.Generic;
using System;
using System.IO;
using WindowsInput.Native;
using WindowsInput;

[assembly: System.Reflection.AssemblyTitle("WiiBalanceScale")]
[assembly: System.Reflection.AssemblyProduct("WiiBalanceScale")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace WiiBalanceScale
{
    public struct Keys{
        public Keys(bool x, bool z){
            this.x=x;
            this.z=z;
        }
        public bool x;
        public bool z;
        public override string ToString()
        {
            List<string> pressing = new List<string>();
            if(x){
                pressing.Add("X");
            }
            if(z){
                pressing.Add("Z");
            }
            return String.Join(", ",pressing);
        }
    }
    public struct BoardValues{
        public BoardValues(int wl, int wr, float bal){
            weightLeft=wl;
            weightRight=wr;
            balance=bal;
        }
        public int weightLeft;
        public int weightRight;
        public float balance;
        public override string ToString()
        {
            return weightLeft + " " + weightRight + " " + (int)balance;
        }
    }
    public class ControlData{
        public float sensorOffsetTopLeft;
        public float sensorOffsetTopRight;
        public float sensorOffsetBottomLeft;
        public float sensorOffsetBottomRight;
        public float balanceOffset;
        public float oneFootLeft;
        public float oneFootRight;
        public float defaultWeightLeft;
        public float defaultWeightRight;
        private static float leanThreshold = 0.2f;
        private static int weightThreshold = 15;
        private bool holdingLeft=false;
        private bool holdingRight=false;
        private float defaultTotalWeight;
        private List<bool> lastLeftValues;
        private List<bool> lastRightValues;
        private static int totalLastValues=3;
        private static int lastValueThreshold = 3;
        private static int totalWeightThreshold= 2;
        private bool bothDown = false;
        private int bothDownCooldown = 0;
        private static int bothDownMaxCooldown = 9;
        public ControlData(float sensorOffsetTopLeft, float sensorOffsetTopRight, float sensorOffsetBottomLeft, float sensorOffsetBottomRight, float balanceOffset, float oneFootLeft, float oneFootRight, float defaultWeightLeft, float defaultWeightRight){
            this.sensorOffsetTopLeft=sensorOffsetTopLeft;
            this.sensorOffsetTopRight=sensorOffsetTopRight;
            this.sensorOffsetBottomLeft=sensorOffsetBottomLeft;
            this.sensorOffsetBottomRight=sensorOffsetBottomRight;
            this.balanceOffset=balanceOffset;
            this.oneFootLeft=oneFootLeft;
            this.oneFootRight=oneFootRight;
            this.defaultWeightLeft=defaultWeightLeft;
            this.defaultWeightRight=defaultWeightRight;
            defaultTotalWeight = (defaultWeightLeft+defaultWeightRight)/2.0f;
            lastLeftValues = new List<bool>();
            lastRightValues = new List<bool>();
            for(int i = 0; i < totalLastValues; i++){
                lastLeftValues.Add(false);
                lastRightValues.Add(false);
            }
        }
        public Keys GetKeys(float tl, float tr, float bl, float br, bool rolling){
            BoardValues values = GetBoardValues(tl,tr,bl,br);
            if(rolling){
                Console.WriteLine(values.balance);
                if(values.balance>=leanThreshold){
                    return new Keys(false,true);
                }
                if(values.balance <= -leanThreshold){
                    return new Keys(true,false);
                }
            }else{
                float totalWeight = (values.weightLeft+values.weightRight)/2.0f;
                lastLeftValues.RemoveAt(0);
                lastRightValues.RemoveAt(0);
                bool l = values.weightLeft > defaultWeightLeft + weightThreshold;
                bool r = values.weightRight > defaultWeightRight + weightThreshold;
                if(l && r && bothDownCooldown <= 0){
                    lastLeftValues.Add(true);
                    lastRightValues.Add(true);
                    bothDown = true;
                }else if(bothDown){
                    lastLeftValues.Add(values.weightLeft > oneFootLeft);
                    lastRightValues.Add(values.weightRight > oneFootRight);
                }
                else
                {
                    bool l2 = l && (totalWeight > defaultTotalWeight - totalWeightThreshold);
                    bool r2 = r && (totalWeight > defaultTotalWeight - totalWeightThreshold);
                    if(bothDownCooldown>0 && l2 && r2)
                    {
                        lastLeftValues.Add(false);
                        lastRightValues.Add(false);
                    }
                    else
                    {
                        lastLeftValues.Add(l2);
                        lastRightValues.Add(r2);
                    }
                }
                holdingLeft=true;
                holdingRight=true;
                int count = 0;
                foreach(bool val in lastLeftValues){
                    if(!val){
                        count++;
                        if(count>=lastValueThreshold){
                            holdingLeft=false;
                            if (bothDown)
                            {
                                bothDown = false;
                                bothDownCooldown = bothDownMaxCooldown;
                            }
                            break;
                        }
                    }
                }
                count=0;
                foreach(bool val in lastRightValues){
                    if(!val){
                        count++;
                        if(count>=lastValueThreshold){
                            holdingRight=false;
                            if (bothDown)
                            {
                                bothDown = false;
                                bothDownCooldown = bothDownMaxCooldown;
                            }
                            break;
                        }
                    }
                }
                if (bothDownCooldown > 0)
                {
                    bothDownCooldown--;
                }
                return new Keys(holdingLeft,holdingRight);
            }
            return new Keys(false,false);
        }
        public BoardValues GetBoardValues(float tl, float tr, float bl, float br){
            float Kx = (tl +bl) / (tr + br);
            return new BoardValues((int)((tl+sensorOffsetTopLeft+bl+sensorOffsetBottomLeft)/2.0f),(int)((tr+sensorOffsetTopRight+br+sensorOffsetBottomRight)/2.0f),((float)(Kx - 1) / (float)(Kx + 1)) * (float)(-43 / 2) + balanceOffset);
        }
    }
    internal class WiiBalanceScale
    {
        static WiiBalanceScaleForm f = null;
        static Wiimote bb = null;
        static ConnectionManager cm = null;
        static Timer BoardTimer = null;

        static List<float> historyTopLeft;
        static List<float> historyTopRight;
        static List<float> historyBottomLeft;
        static List<float> historyBottomRight;
        static int calibrationCount = 0;
        static int calibrationIndex = -1;
        static float sensorOffsetTopLeft;
        static float sensorOffsetTopRight;
        static float sensorOffsetBottomLeft;
        static float sensorOffsetBottomRight;
        static float balanceOffset;
        static float oneFootLeft;
        static float oneFootRight;
        static float defaultWeightLeft;
        static float defaultWeightRight;
        static bool rollMode = false;
        static ControlData controller;
        static InputSimulator sim;
        static Keys currentlyDown;

        static void Main(string[] args)
        {
            currentlyDown = new Keys(false, false);
            sim = new InputSimulator();
            if(File.Exists("calibration.txt")){
                StreamReader file = new StreamReader(@"calibration.txt");
                sensorOffsetTopLeft = float.Parse(file.ReadLine());
                sensorOffsetTopRight = float.Parse(file.ReadLine());
                sensorOffsetBottomLeft = float.Parse(file.ReadLine());
                sensorOffsetBottomRight = float.Parse(file.ReadLine());
                balanceOffset = float.Parse(file.ReadLine());
                oneFootLeft = float.Parse(file.ReadLine());
                oneFootRight = float.Parse(file.ReadLine());
                defaultWeightLeft = float.Parse(file.ReadLine());
                defaultWeightRight = float.Parse(file.ReadLine());
                file.Close();
                Console.WriteLine(defaultWeightLeft + " " + defaultWeightRight);
                controller = new ControlData(sensorOffsetTopLeft,sensorOffsetTopRight,sensorOffsetBottomLeft,sensorOffsetBottomRight,balanceOffset,oneFootLeft,oneFootRight,defaultWeightLeft,defaultWeightRight);
            }else{
                calibrationIndex=0;
                controller = new ControlData(0,0,0,0,0,0,0,80,80);
            }
            Console.WriteLine(defaultWeightRight);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            f = new WiiBalanceScaleForm();
            f.lblWeight.Text = "";
            f.lblQuality.Text = "";
            f.lblUnit.Text = "";
            f.btnReset.Click += new System.EventHandler(btnReset_Click);
            f.lblUnit.Click += new System.EventHandler(lblUnit_Click);

            ConnectBalanceBoard(false);
            if (f == null) return; //connecting required application restart, end this process here

            BoardTimer = new System.Windows.Forms.Timer();
            BoardTimer.Interval = 50;
            BoardTimer.Tick += new System.EventHandler(BoardTimer_Tick);
            BoardTimer.Start();

            Application.Run(f);
            Shutdown();
        }

        static void Shutdown()
        {
            if (BoardTimer != null) { BoardTimer.Stop(); BoardTimer = null; }
            if (cm != null) { cm.Cancel(); cm = null; }
            if (f != null) { if (f.Visible) f.Close(); f = null; }
        }

        static void ConnectBalanceBoard(bool WasJustConnected)
        {
            bool Connected = true; try { bb = new Wiimote(); bb.Connect(); bb.SetLEDs(1); bb.GetStatus(); } catch { Connected = false; }

            if (!Connected || bb.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                if (ConnectionManager.ElevateProcessNeedRestart()) { Shutdown(); return; }
                if (cm == null) cm = new ConnectionManager();
                cm.ConnectNextWiiMote();
                return;
            }
            if (cm != null) { cm.Cancel(); cm = null; }

            f.lblWeight.Text = "...";
            f.lblQuality.Text = "";
            f.lblUnit.Text = "";
            f.Refresh();

        }

        static void BoardTimer_Tick(object sender, System.EventArgs e)
        {
            if (cm != null)
            {
                if (cm.IsRunning())
                {
                    f.lblWeight.Text = "Connecting...";
                    f.lblQuality.Text = (f.lblQuality.Text.Length >= 5 ? "" : f.lblQuality.Text) + "6";
                    return;
                }
                if (cm.HadError())
                {
                    BoardTimer.Stop();
                    System.Windows.Forms.MessageBox.Show(f, "No compatible bluetooth adapter found - Quitting", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Shutdown();
                    return;
                }
                ConnectBalanceBoard(true);
                return;
            }

            float kg = bb.WiimoteState.BalanceBoardState.WeightKg;
            if (kg < -200)
            {
                ConnectBalanceBoard(false);
                return;
            }
            float rightSensor = (int)((bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopRight + bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomRight)/2.0f);
            float leftSensor = (int)((bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopLeft + bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomLeft)/2.0f);
            switch(calibrationIndex){
                case 0:
                    //Initialize Stand off Board
                    f.lblWeight.Text = "Step off the board...";
                    calibrationIndex=-2;
                    DelayAction(1500,delegate(){
                        f.lblWeight.Text = "Recording...";
                        RecordValues(100,true);
                        calibrationIndex=1;
                    });
                    break;
                case 1:
                    if(RecordValues(100,false)){
                        sensorOffsetTopLeft = -Average(historyTopLeft);
                        sensorOffsetTopRight = -Average(historyTopRight);
                        sensorOffsetBottomLeft = -Average(historyBottomLeft);
                        sensorOffsetBottomRight = -Average(historyBottomRight);
                        Console.WriteLine(sensorOffsetTopLeft + " " + sensorOffsetTopRight + " " + sensorOffsetBottomLeft + " " + sensorOffsetBottomRight);
                        f.lblWeight.Text = "Step onto the board. Stay balanced.";
                        calibrationIndex=-2;
                        DelayAction(3000,delegate(){
                            f.lblWeight.Text = "Recording...";
                            RecordValues(100,true);
                            calibrationIndex=2;
                        });
                    }
                    break;
                case 2:
                    if(RecordValues(100,false)){
                        float wtl = Average(historyTopLeft)+sensorOffsetTopLeft;
                        float wtr = Average(historyTopRight)+sensorOffsetTopRight;
                        float wbl = Average(historyBottomLeft)+sensorOffsetBottomLeft;
                        float wbr = Average(historyBottomRight)+sensorOffsetBottomRight;
                        defaultWeightLeft = (wtl+wbl)/2.0f;
                        defaultWeightRight = (wtr+wbr)/2.0f;
                        float Kx = (wtl+wbl)/(wtr+wbr);
                        balanceOffset = -((float)(Kx - 1) / (float)(Kx + 1)) * (float)(-43 / 2);
                        Console.WriteLine(balanceOffset);
                        f.lblWeight.Text = "Remove your left foot.";
                        calibrationIndex=-2;
                        DelayAction(2000,delegate(){
                            f.lblWeight.Text = "Recording...";
                            RecordValues(100,true);
                            calibrationIndex=3;
                        });
                    }
                    break;
                case 3:
                    if(RecordValues(100,false)){
                        oneFootLeft = (Average(historyTopLeft)+sensorOffsetTopLeft + Average(historyBottomLeft)+sensorOffsetBottomLeft)/2.0f;
                        Console.WriteLine(oneFootLeft);
                        f.lblWeight.Text = "Remove your right foot.";
                        calibrationIndex=-2;
                        DelayAction(3500,delegate(){ 
                            f.lblWeight.Text = "Recording...";
                            RecordValues(100,true);
                            calibrationIndex=4;
                        });
                    }
                    break;
                case 4:
                    if(RecordValues(100,false)){
                        oneFootRight = (Average(historyTopRight)+sensorOffsetTopRight + Average(historyBottomRight)+sensorOffsetBottomRight)/2.0f;
                        Console.WriteLine(oneFootRight);
                        f.lblWeight.Text = "Calibration Complete. Step onto the board.";
                        calibrationIndex=-2;
                        File.WriteAllText("calibration.txt",sensorOffsetTopLeft+ "\n" + sensorOffsetTopRight + "\n" + sensorOffsetBottomLeft + "\n" + sensorOffsetBottomRight + "\n" + balanceOffset + "\n" + oneFootLeft + "\n" + oneFootRight + "\n" + defaultWeightLeft + "\n" + defaultWeightRight);
                        controller = new ControlData(sensorOffsetTopLeft,sensorOffsetTopRight,sensorOffsetBottomLeft,sensorOffsetBottomRight,balanceOffset,oneFootLeft,oneFootRight,defaultWeightLeft,defaultWeightRight);
                        DelayAction(3000,delegate(){
                            f.lblWeight.Text = "Back to Gameplay!";
                            calibrationIndex=-1;
                        });
                    }
                    break;
                case -1:
                    Keys keys = controller.GetKeys(bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopLeft, bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopRight, bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomLeft, bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomRight, false);
                    string displayTxt = (rollMode?"Mode: Roll\n":"Mode: Jump\n") + keys + "\n" + controller.GetBoardValues(bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopLeft,bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopRight,bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomLeft,bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomRight);
                    if(keys.x && !currentlyDown.x)
                    {
                        sim.Keyboard.KeyDown(VirtualKeyCode.VK_Z);
                    }else if(!keys.x && currentlyDown.x)
                    {
                        sim.Keyboard.KeyUp(VirtualKeyCode.VK_Z);
                    }
                    if(keys.z && !currentlyDown.z)
                    {
                        sim.Keyboard.KeyDown(VirtualKeyCode.VK_X);
                    }else if(!keys.z && currentlyDown.z)
                    {
                        sim.Keyboard.KeyUp(VirtualKeyCode.VK_X);
                    }
                    currentlyDown = keys;
                    f.lblWeight.Text = displayTxt;
                    break;
            }
        }
        //True for done, false for not yet done.
        static bool RecordValues(int maxValuesRecorded, bool initial){
            if(initial){
                historyTopLeft = new List<float>();
                historyTopRight = new List<float>();
                historyBottomLeft = new List<float>();
                historyBottomRight = new List<float>();
                calibrationCount = 0;
            }
            historyTopLeft.Add(bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopLeft);
            historyTopRight.Add(bb.WiimoteState.BalanceBoardState.SensorValuesKg.TopRight);
            historyBottomLeft.Add(bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomLeft);
            historyBottomRight.Add(bb.WiimoteState.BalanceBoardState.SensorValuesKg.BottomRight);
            calibrationCount++;
            return calibrationCount >= maxValuesRecorded;
        }

        static void btnReset_Click(object sender, System.EventArgs e)
        {
            //Begin calibration process
            Console.WriteLine("Hi!");
            calibrationIndex=0;
        }

        static void lblUnit_Click(object sender, System.EventArgs e)
        {
            
        }
        public static void DelayAction(int millisecond, Action action)
        {
            var timer = new Timer();
            timer.Tick += delegate

            {
                action.Invoke();
                timer.Stop();
            };

            timer.Interval = millisecond;
            timer.Start();
        }
        public static float Average(List<float> items){
            float sum = 0;
            foreach(float num in items){
                sum+=num;
            }
            return sum/items.Count;
        }
    }
}