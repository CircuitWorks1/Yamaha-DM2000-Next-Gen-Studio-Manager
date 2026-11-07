using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Receive-only transport: deliberately contains no MIDI output API.
public sealed class MidiInput : IDisposable {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Caps {
  public ushort manufacturer, product; public uint version;
  [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string name; public uint support;
 }
 [StructLayout(LayoutKind.Sequential)] struct Header {
  public IntPtr data; public uint length, recorded; public UIntPtr user;
  public uint flags; public IntPtr next; public UIntPtr reserved; public uint offset;
  [MarshalAs(UnmanagedType.ByValArray, SizeConst=8)] public UIntPtr[] padding;
 }
 delegate void Callback(IntPtr h,uint msg,UIntPtr instance,UIntPtr p1,UIntPtr p2);
 [DllImport("winmm.dll")] static extern uint midiInGetNumDevs();
 [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern uint midiInGetDevCapsW(UIntPtr id,out Caps caps,uint size);
 [DllImport("winmm.dll")] static extern uint midiInOpen(out IntPtr h,uint id,Callback cb,UIntPtr instance,uint flags);
 [DllImport("winmm.dll")] static extern uint midiInPrepareHeader(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint midiInAddBuffer(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint midiInUnprepareHeader(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint midiInStart(IntPtr h);
 [DllImport("winmm.dll")] static extern uint midiInStop(IntPtr h);
 [DllImport("winmm.dll")] static extern uint midiInReset(IntPtr h);
 [DllImport("winmm.dll")] static extern uint midiInClose(IntPtr h);
 [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern uint midiInGetErrorTextW(uint code,StringBuilder text,uint length);
 public static SortedDictionary<uint,string> Devices(){var list=new SortedDictionary<uint,string>();for(uint i=0;i<midiInGetNumDevs();i++){Caps c;if(midiInGetDevCapsW((UIntPtr)i,out c,(uint)Marshal.SizeOf(typeof(Caps)))==0)list[i]=c.name;}return list;}
 static void Check(uint n){if(n==0)return;var text=new StringBuilder(256);midiInGetErrorTextW(n,text,256);throw new InvalidOperationException(text.ToString()+" ("+n+")");}
 IntPtr handle; Callback callback; volatile bool closing; readonly List<IntPtr> buffers=new List<IntPtr>();
 readonly ConcurrentQueue<IntPtr> returned=new ConcurrentQueue<IntPtr>();
 public readonly ConcurrentQueue<byte[]> Packets=new ConcurrentQueue<byte[]>(); public readonly ConcurrentQueue<string> Messages=new ConcurrentQueue<string>();
 readonly List<byte> frame=new List<byte>();
 static readonly uint Size=(uint)Marshal.SizeOf(typeof(Header));
 public MidiInput(uint id){callback=Receive;Check(midiInOpen(out handle,id,callback,UIntPtr.Zero,0x30000));try{for(int i=0;i<64;i++){var header=new Header{data=Marshal.AllocHGlobal(16384),length=16384,padding=new UIntPtr[8]};IntPtr p=Marshal.AllocHGlobal((int)Size);Marshal.StructureToPtr(header,p,false);uint result=midiInPrepareHeader(handle,p,Size);if(result!=0){Marshal.FreeHGlobal(header.data);Marshal.FreeHGlobal(p);Check(result);}buffers.Add(p);Check(midiInAddBuffer(handle,p,Size));}Check(midiInStart(handle));}catch{Dispose();throw;}}
 void Receive(IntPtr h,uint msg,UIntPtr instance,UIntPtr p1,UIntPtr p2){try{if(msg==0x3C4||msg==0x3C6){IntPtr p=(IntPtr)p1.ToUInt64();var hdr=(Header)Marshal.PtrToStructure(p,typeof(Header));if(!closing){byte[] bytes=new byte[(int)Math.Min(hdr.recorded,hdr.length)];Marshal.Copy(hdr.data,bytes,0,bytes.Length);if(msg==0x3C6){frame.Clear();Enqueue("ERROR: incomplete MIDI buffer");}else foreach(byte b in bytes){if(b>=0xF8)continue;if(b==0xF0)frame.Clear();if(frame.Count>0||b==0xF0)frame.Add(b);if(b==0xF7&&frame.Count>0){byte[] packet=frame.ToArray();frame.Clear();if(Packets.Count<2048)Packets.Enqueue(packet);Enqueue(Describe(packet)+" | "+BitConverter.ToString(packet).Replace('-',' '));}if(frame.Count>1048576){frame.Clear();Enqueue("ERROR: oversized SysEx discarded");}}returned.Enqueue(p);}}else if(msg==0x3C3&&!closing){uint packed=(uint)p1.ToUInt64();byte status=(byte)packed;if(status>=0xF8)return;Enqueue("MIDI | "+status.ToString("X2")+" "+((packed>>8)&127).ToString("X2")+" "+((packed>>16)&127).ToString("X2"));}}catch(Exception e){Enqueue("Receive error: "+e.Message);}}
 void Enqueue(string s){if(Messages.Count<2048)Messages.Enqueue(DateTimeOffset.Now.ToString("o")+" "+s);}
 public static string Describe(byte[] p){if(p.Length<2||p[0]!=0xF0||p[p.Length-1]!=0xF7)return "Invalid SysEx";if(p.Length>=10&&p[1]==0x43&&p[3]==0x3E&&(p[4]==6||p[4]==127)){string type=p[5]==3?"Setup":p[5]==4?"Backup":p[5]==1?"Edit buffer":p[5]==0x21?"Meter":"Type "+p[5].ToString("X2");return "Yamaha "+(p[4]==6?"DM2000":"universal")+" "+type+" | device "+((p[2]&15)+1)+" | address "+p[6].ToString("X2")+" "+p[7].ToString("X2")+" "+p[8].ToString("X2");}return "SysEx ("+p.Length+" bytes)";}
 public void Pump(){IntPtr p;while(returned.TryDequeue(out p)){if(!closing)Check(midiInAddBuffer(handle,p,Size));}}
 public void Dispose(){if(handle==IntPtr.Zero)return;closing=true;midiInStop(handle);midiInReset(handle);foreach(IntPtr p in buffers){uint r=midiInUnprepareHeader(handle,p,Size);if(r==0){var hdr=(Header)Marshal.PtrToStructure(p,typeof(Header));Marshal.FreeHGlobal(hdr.data);Marshal.FreeHGlobal(p);}}buffers.Clear();midiInClose(handle);handle=IntPtr.Zero;GC.KeepAlive(callback);}
}

sealed class Port {public uint Id;public string Name;public override string ToString(){return Name;}}
sealed partial class ChannelStrip : Control {
 public int Number; public bool Selected; public int? Raw;public int? Meter;public string MeterPoint="Unknown"; public event Action<int> Pick;
 public ChannelStrip(int n){Number=n;DoubleBuffered=true;TabStop=true;InstallPan();InstallMixerButtons();MinimumSize=new Size(58,264);Cursor=Cursors.Hand;AccessibleName="Input "+n+", settings not synchronized";}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(Pick!=null)Pick(Number);PressFader(e);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.Clear(Selected?Color.FromArgb(36,65,82):Color.FromArgb(32,38,46));using(var pen=new Pen(Color.FromArgb(100,177,205),3))if(Selected)g.DrawLine(pen,0,1,Width,1);using(var small=new Font(Font.FontFamily,7))using(var fg=new SolidBrush(Color.FromArgb(224,232,242)))using(var muted=new SolidBrush(Color.FromArgb(151,167,183)))using(var track=new SolidBrush(Color.FromArgb(15,20,26)))using(var fader=new SolidBrush(Color.FromArgb(89,103,119))){g.DrawString(DisplayTitle??("Input "+Number),small,muted,5,8);TextRenderer.DrawText(g,NameText,Font,new Rectangle(5,24,Width-10,20),Color.FromArgb(225,233,243),TextFormatFlags.EndEllipsis);TextRenderer.DrawText(g,SourceText,small,new Rectangle(5,41,Width-10,14),Color.FromArgb(160,177,193),TextFormatFlags.EndEllipsis);g.DrawString(MeterPoint.ToUpperInvariant(),small,muted,5,117);int h=Math.Max(60,Height-205);g.FillRectangle(track,(Width-18)/2-3,145,6,h);g.FillRectangle(track,Width-13,145,10,h);if(Meter.HasValue){double db=MeterProtocol.Decibels(Meter.Value);float fill=(float)(Math.Max(0,Math.Min(1,(db+96)/96))*h);using(var signal=new SolidBrush(Meter.Value>=8191?Color.FromArgb(239,94,94):db>=-6?Color.FromArgb(232,192,91):Color.FromArgb(76,196,151))){g.FillRectangle(signal,Width-13,145+h-fill,10,fill);}for(int tick=0;tick<=4;tick++)g.DrawLine(Pens.Gray,Width-16,145+tick*h/4,Width-14,145+tick*h/4);}if(DisplayRaw.HasValue)g.FillRectangle(fader,Thumb);g.DrawString(DisplayLevel,Font,Raw.HasValue?fg:muted,new RectangleF(3,Height-52,Width-6,20));PaintStereoMeters(g);}}
}
sealed partial class Studio : Form {
 ComboBox ports=new ComboBox(),banks=new ComboBox(),dock=new ComboBox();Button connect=new Button();Label state=new Label();TextBox log=new TextBox();Panel inspector=new Panel();FaderConnection midi; int device; readonly int?[] faders=new int?[96]; readonly DateTime[] updated=new DateTime[96]; readonly int?[] meterValues=new int?[96];readonly DateTime[] meterDates=new DateTime[96];int? inputPoint,outputPoint;readonly int?[] panValues=new int?[96];readonly DateTime[] panDates=new DateTime[96];string outputName;System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();int count,channel=1;StreamWriter capture;string capturePath;
 int? CurrentPan(int index){return midi!=null&&(DateTime.UtcNow-panDates[index]).TotalSeconds<2?panValues[index]:null;}int? CurrentMeter(int index){return midi!=null&&(DateTime.UtcNow-meterDates[index]).TotalSeconds<1?meterValues[index]:null;} int? Current(int index){return midi!=null&&(DateTime.UtcNow-updated[index]).TotalSeconds<6?faders[index]:null;} void Details(){RenderDetails();} void RefreshPorts(){ports.Items.Clear();foreach(var p in MidiInput.Devices())if(p.Value.IndexOf("DM2000",StringComparison.OrdinalIgnoreCase)>=0)ports.Items.Add(new Port{Id=p.Key,Name=p.Value});if(ports.Items.Count>0)ports.SelectedIndex=0;string config=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"connection.ini");if(File.Exists(config)){var lines=File.ReadAllLines(config);if(lines.Length>=3){outputName=lines[1];int.TryParse(lines[2],out device);for(int i=0;i<ports.Items.Count;i++)if(((Port)ports.Items[i]).Name==lines[0])ports.SelectedIndex=i;}}state.Text=ports.Items.Count+" DM2000 input ports detected · Select the port configured on the console";}
 void Connect(){try{var p=ports.SelectedItem as Port;if(p==null)throw new Exception("No DM2000 input port selected. Check USB and the Yamaha driver, then Refresh.");string target=outputName??p.Name;uint outputId=0;bool opened=false;foreach(var o in FaderReader.Devices())if(o.Value==target){outputId=o.Key;opened=true;break;}if(!opened)throw new Exception("Matching read-request output not found.");Array.Clear(faders,0,faders.Length);Array.Clear(updated,0,updated.Length);midi=new FaderConnection(p.Id,outputId,device);midi.SetMixerView(banks.SelectedIndex,SelectedAux);string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"captures");Directory.CreateDirectory(dir);capturePath=Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log");capture=new StreamWriter(capturePath,false,Encoding.UTF8){AutoFlush=false};ports.Enabled=false;connect.Text="Disconnect";state.Text="Connected · "+p.Name+" · Live faders + meters · Fader + pan control enabled";Append("Connected. Fader and pan controls update the console; live readback and metering remain active. If busy, close Studio Manager and retry.");}catch(Exception e){Disconnect();state.Text="Connection failed: "+e.Message;}}
 void Append(string s){if(log.TextLength>100000)log.Clear();log.AppendText(s+Environment.NewLine);if(capture!=null)capture.WriteLine(s);}
 void Drain(){if(midi==null)return;try{if(midi.Error!=null)throw new IOException(midi.Error);midi.Copy(faders,updated);midi.CopyPan(panValues,panDates);midi.CopyMeters(meterValues,meterDates,out inputPoint,out outputPoint);RefreshMixer();string line;int n=0;while(n++<200&&midi.Messages.TryDequeue(out line)){count++;Append(line);}Details();}catch(Exception e){Disconnect();state.Text="Receive stopped: "+e.Message;}}
 void Disconnect(){if(midi!=null){midi.Dispose();midi=null;}if(capture!=null){capture.Dispose();capture=null;}Array.Clear(faders,0,faders.Length);Array.Clear(meterValues,0,96);Array.Clear(panValues,0,96);Array.Clear(panDates,0,96);Array.Clear(meterDates,0,96);inputPoint=outputPoint=null;foreach(ChannelStrip strip in strips.Controls){strip.RejectEdit();strip.PanSlider.SetConfirmed(null);strip.Raw=null;strip.Meter=null;strip.MeterPoint="Unknown";strip.Invalidate();}ResetMixer();Details();ports.Enabled=true;connect.Text="Connect";state.Text="Disconnected · Fader control unavailable";}
 [STAThread] public static void Main(string[] args){if(args.Length>0&&args[0]=="--routing-test"){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);RoutingWindow.Test();return;}if(args.Length>0&&args[0]=="--source-display-test"){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TestSourceDisplayStable();return;}if(args.Length>0&&args[0]=="--startup-source-test"){FaderConnection.VerifyStartupHardware();return;}if(args.Length>0&&args[0]=="--card-ui-test"){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TestInspectorCard();return;}if(args.Length>0&&args[0]=="--source-test"){InputSources.Probe();return;}if(args.Length>0&&args[0]=="--stereo-test"){FeatureTests.Stereo();return;}if(args.Length>0&&args[0]=="--expanded-ui-test"){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TestExpandedUi();return;}if(args.Length>0&&args[0]=="--feature-writes"){FeatureTests.Writes();return;}if(args.Length>0&&args[0]=="--aux-all"){FeatureTests.AuxAll();return;}if(args.Length>0&&args[0]=="--aux-probe"){FeatureTests.AuxProbe();return;}if(args.Length>0&&args[0]=="--features-test"){FeatureTests.Read();return;}if(args.Length>0&&args[0]=="--pan-write-test"){PanProtocol.HardwareTest();return;}if(args.Length>0&&args[0]=="--pan-probe"){PanProtocol.Probe();return;}if(args.Length>0&&args[0]=="--write-test"){FaderWriteProtocol.HardwareTest();return;}if(args.Length>0&&args[0]=="--layout-test"){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TestLayouts();return;}if(args.Length>0&&args[0]=="--meter-probe"){MeterProtocol.Probe();return;}if(args.Length>0&&args[0]=="--performance-test"){LatencyTest.Continuous();return;}if(args.Length>0&&args[0]=="--latency-test"){LatencyTest.Run();return;}if(args.Length>0&&args[0]=="--fader-probe"){FaderReader.Probe();return;}if(args.Length>0&&args[0]=="--probe"){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"connection-report.txt");using(var w=new StreamWriter(path)){foreach(var p in MidiInput.Devices()){w.WriteLine(p.Key+": "+p.Value);if(p.Value.IndexOf("DM2000",StringComparison.OrdinalIgnoreCase)<0)continue;try{using(var input=new MidiInput(p.Key)){int total=0;for(int i=0;i<20;i++){Thread.Sleep(100);input.Pump();string line;while(input.Messages.TryDequeue(out line)){w.WriteLine(line);total++;}}w.WriteLine("Receive-only open/start/close OK; messages: "+total);}}catch(Exception e){w.WriteLine("Open failed: "+e.Message);}}}return;}if(args.Length>0&&args[0]=="--self-test"){ZeroInputOperation.Test();FaderConnection.TestStartupSnapshot();CardLabels.Test();InputSources.Test();StereoLink.Test();ChannelStrip.TestMixerButtons();MixerParameter.Test();PanProtocol.Test();PanControl.Test();FaderProtocol.Test();FaderWriteProtocol.Test();ChannelStrip.TestGesture();MeterProtocol.Test();string a=MidiInput.Describe(new byte[]{0xF0,0x43,0x10,0x3E,6,3,0,0,1,2,0xF7});if(!a.Contains("DM2000 Setup")||MidiInput.Describe(new byte[]{0xF0,0x43})!="Invalid SysEx")throw new Exception("Protocol parser failed");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),"Parser checks passed");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Length>0&&args[0]=="--hardware-test"){using(var app=new Studio())using(var report=new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hardware-test.txt"))){app.Show();app.Connect();if(app.midi==null)throw new Exception("Hardware connection failed");for(int bank=0;bank<4;bank++){app.banks.SelectedIndex=bank;DateTime end=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}for(int i=bank*24;i<bank*24+24;i++){if(!app.Current(i).HasValue||!app.CurrentPan(i).HasValue)throw new Exception("Missing fader or pan reply for input "+(i+1));report.WriteLine("Input "+(i+1)+": "+FaderProtocol.Label(app.Current(i).Value));}}app.banks.SelectedIndex=0;DateTime finish=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<finish){Application.DoEvents();Thread.Sleep(10);}using(var image=new Bitmap(app.Width,app.Height)){app.DrawToBitmap(image,new Rectangle(0,0,app.Width,app.Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"live-preview.png"));}app.meterDates[0]=DateTime.UtcNow.AddSeconds(-2);if(app.CurrentMeter(0).HasValue)throw new Exception("Stale meter remained visible");app.Disconnect();if(app.inputPoint!=null||app.outputPoint!=null)throw new Exception("Meter position survived disconnect");for(int i=0;i<96;i++)if(app.Current(i).HasValue||app.CurrentMeter(i).HasValue||app.CurrentPan(i).HasValue)throw new Exception("Stale value survived disconnect");report.WriteLine("PASS: all 96 input faders and pans read; stale meters hidden; disconnect clears all live values.");app.Close();}return;}if(args.Length>0&&args[0]=="--ui-test"){using(var app=new Studio()){app.Show();Application.DoEvents();for(int b=0;b<4;b++){app.banks.SelectedIndex=b;if(app.strips.Controls.Count!=24)throw new Exception("Bank rendering failed");}for(int d=0;d<3;d++){app.dock.SelectedIndex=d;Application.DoEvents();}app.dock.SelectedIndex=0;app.banks.SelectedIndex=0;using(var image=new Bitmap(app.Width,app.Height)){app.DrawToBitmap(image,new Rectangle(0,0,app.Width,app.Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}app.Close();}return;}var live=new Studio();if(args.Length>0&&args[0]=="--connect")live.Shown+=delegate{live.Connect();};Application.Run(live);}
}





































