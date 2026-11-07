using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

public static class PanProtocol {
 public static void HardwareTest(){string[] config=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"connection.ini"));uint input=0,output=0;foreach(var p in MidiInput.Devices())if(p.Value==config[0])input=p.Key;foreach(var p in FaderReader.Devices())if(p.Value==config[1])output=p.Key;using(var log=new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pan-write-test.txt")))using(var connection=new FaderConnection(input,output,int.Parse(config[2]))){log.AutoFlush=true;int?[] values=new int?[96];DateTime[] dates=new DateTime[96];var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<2000){connection.CopyPan(values,dates);if(values[0].HasValue)break;Thread.Sleep(5);}if(!values[0].HasValue)throw new Exception("No confirmed pan for test");int original=values[0].Value,target=original> -63?original-1:original+1;log.WriteLine("Input 1 original: "+Label(original));bool restored=false;try{connection.WritePan(0,target);watch.Restart();while(watch.ElapsedMilliseconds<2000){connection.CopyPan(values,dates);if(values[0]==target)break;Thread.Sleep(5);}if(values[0]!=target)throw new Exception("Pan write not confirmed");log.WriteLine("Desk confirmed: "+Label(target));connection.CopyPan(values,dates);if(values[0]!=target)throw new Exception("External change; not overwriting");connection.WritePan(0,original);watch.Restart();while(watch.ElapsedMilliseconds<2000){connection.CopyPan(values,dates);if(values[0]==original){restored=true;break;}Thread.Sleep(5);}if(!restored)throw new Exception("Pan restoration not confirmed");log.WriteLine("PASS: original pan restored and verified.");}finally{if(!restored){connection.CopyPan(values,dates);if(values[0]==target){connection.WritePan(0,original);Thread.Sleep(200);log.WriteLine("Restoration requested after failure; verify console.");}}}}}
 public static byte[] Request(int channel,int device){Validate(channel,0,device);return new byte[]{0xF0,0x43,(byte)(0x30|device),0x3E,0x7F,1,0x1B,0,(byte)channel,0xF7};}
 static void Validate(int channel,int value,int device){if(channel<0||channel>95||value< -63||value>63||device<0||device>15)throw new ArgumentOutOfRangeException();}
 public static byte[] Create(int channel,int value,int device){Validate(channel,value,device);return new byte[]{0xF0,0x43,(byte)(0x10|device),0x3E,0x7F,1,0x1B,0,(byte)channel,(byte)((value>>21)&127),(byte)((value>>14)&127),(byte)((value>>7)&127),(byte)(value&127),0xF7};}
 public static bool TryRead(byte[] p,out int channel,out int value){channel=value=0;if(p==null||p.Length!=14||p[0]!=0xF0||p[1]!=0x43||(p[2]&0xF0)!=0x10||p[3]!=0x3E||p[4]!=0x7F||p[5]!=1||p[6]!=0x1B||p[7]!=0||p[8]>95||p[13]!=0xF7)return false;for(int i=9;i<13;i++)if(p[i]>127)return false;int v=(p[9]<<21)|(p[10]<<14)|(p[11]<<7)|p[12];if((v&0x08000000)!=0)v-=0x10000000;if(v< -63||v>63)return false;channel=p[8];value=v;return true;}
 public static string Label(int? value){return !value.HasValue?"Unknown":value==0?"Center":(value<0?"L":"R")+Math.Abs(value.Value);}
 public static void Test(){int c,v;for(int n=0;n<96;n++)for(int pan=-63;pan<=63;pan++){var packet=Create(n,pan,15);if(!TryRead(packet,out c,out v)||c!=n||v!=pan||packet[2]!=0x1F)throw new Exception("Pan roundtrip failed");}byte[] left={0xF0,0x43,0x10,0x3E,0x7F,1,0x1B,0,0,127,127,127,65,0xF7};if(!TryRead(left,out c,out v)||v!=-63)throw new Exception("Signed left-pan decoding failed");left[12]=64;if(TryRead(left,out c,out v))throw new Exception("Out-of-range pan accepted");if(TryRead(Request(0,0),out c,out v))throw new Exception("Pan request decoded as data");foreach(int[] args in new int[][]{new int[]{-1,0,0},new int[]{96,0,0},new int[]{0,-64,0},new int[]{0,64,0},new int[]{0,0,16}}){bool rejected=false;try{Create(args[0],args[1],args[2]);}catch(ArgumentOutOfRangeException){rejected=true;}if(!rejected)throw new Exception("Invalid pan write accepted");}}
 public static void Probe(){string[] config=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"connection.ini"));uint input=0,output=0;foreach(var p in MidiInput.Devices())if(p.Value==config[0])input=p.Key;foreach(var p in FaderReader.Devices())if(p.Value==config[1])output=p.Key;using(var log=new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pan-probe.txt")))using(var source=new MidiInput(input))using(var sink=new FaderReader(output)){for(int i=0;i<96;i++)sink.ReadPan(i,int.Parse(config[2]));var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<1000){source.Pump();byte[] packet;while(source.Packets.TryDequeue(out packet)){int c,v;if(TryRead(packet,out c,out v))log.WriteLine("Input "+(c+1)+": "+Label(v)+" | "+BitConverter.ToString(packet));}Thread.Sleep(1);}}}
}

sealed class PanControl : UserControl {
 readonly TrackBar slider=new TrackBar();readonly Label caption=new Label();bool editing;int? confirmed,pending;DateTime pendingAt;
 int target=-1;public event Action<int> Edited;public void SetTarget(int channel){if(target==channel)return;target=channel;slider.Capture=false;pending=null;editing=false;confirmed=null;slider.Enabled=false;}
 public PanControl(){Height=52;MinimumSize=new Size(46,48);BackColor=Color.FromArgb(32,38,46);ForeColor=Color.FromArgb(225,233,243);caption.Dock=DockStyle.Top;caption.Height=20;caption.TextAlign=ContentAlignment.MiddleCenter;caption.Text=Width<90?"Pan —":"Pan · Unknown";slider.Dock=DockStyle.Fill;slider.Minimum=-63;slider.Maximum=63;slider.TickStyle=TickStyle.None;slider.AutoSize=false;slider.SmallChange=1;slider.LargeChange=5;slider.Enabled=false;slider.BackColor=BackColor;slider.AccessibleName="Pan, left 63 to right 63";Controls.Add(slider);Controls.Add(caption);slider.Scroll+=delegate{Request(slider.Value);};slider.MouseDown+=delegate{editing=true;};slider.MouseUp+=delegate{editing=false;};slider.MouseCaptureChanged+=delegate{if(!slider.Capture)editing=false;};caption.DoubleClick+=delegate{Center();};}
 public void SetConfirmed(int? value){confirmed=value;slider.Enabled=value.HasValue;if(!value.HasValue){slider.Capture=false;pending=null;editing=false;caption.Text=Width<90?"Pan —":"Pan · Unknown";return;}if(pending==value||(DateTime.UtcNow-pendingAt).TotalSeconds>=1)pending=null;if(!editing&&!pending.HasValue&&slider.Value!=value.Value)slider.Value=value.Value;int? shown=editing||pending.HasValue?(int?)slider.Value:value;caption.Text="Pan · "+(Width<90&&shown==0?"C":PanProtocol.Label(shown));}
 void Request(int value){if(!confirmed.HasValue)return;pending=value;pendingAt=DateTime.UtcNow;caption.Text="Pan · "+PanProtocol.Label(value);if(Edited!=null)Edited(value);}
 public void Center(){if(!confirmed.HasValue)return;slider.Value=0;Request(0);}
 public void Reject(){pending=null;editing=false;SetConfirmed(confirmed);}
 public static void Test(){using(var pan=new PanControl()){int edits=0,last=100;pan.Edited+=delegate(int n){edits++;last=n;};pan.Center();if(edits!=0)throw new Exception("Offline pan edited");pan.SetConfirmed(-63);if(edits!=0)throw new Exception("Pan readback caused write");pan.Center();if(edits!=1||last!=0)throw new Exception("Center did not issue a pan write");pan.SetConfirmed(0);pan.SetConfirmed(63);if(edits!=1)throw new Exception("Pan readback echoed a write");pan.SetConfirmed(null);pan.Center();if(edits!=1)throw new Exception("Disconnected pan edited");}}
}

sealed partial class ChannelStrip {
 public readonly PanControl PanSlider=new PanControl();public event Action<int,int> PanChanged;
 void InstallPan(){Controls.Add(PanSlider);PanSlider.Edited+=delegate(int value){if(Pick!=null)Pick(Number);if(PanChanged!=null)PanChanged(Number,value);};PositionPan();}
 void PositionPan(){PanSlider.Bounds=new Rectangle(4,61,Math.Max(46,Width-8),52);}
 protected override void OnResize(EventArgs e){base.OnResize(e);if(PanSlider!=null)PositionPan();PlaceMixerButtons();}
}





