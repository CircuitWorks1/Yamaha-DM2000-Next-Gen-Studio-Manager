using System;
using System.Windows.Forms;

// One channel at a time: fresh read, write, then separate readback confirmation.
sealed class ZeroInputOperation {
 public const int Unity=823;public int Completed{get;private set;}public bool Busy{get;private set;}public string Status{get;private set;}
 int phase;double deadline,nextRead;DateTime requested;
 public ZeroInputOperation(){Busy=true;Status="Zeroing inputs · 0/96";}
 public void Cancel(){Busy=false;Status="Zero stopped · "+Completed+"/96 confirmed. Completed changes are retained.";}
 public void Tick(double now,DateTime utc,int? level,DateTime levelDate,int? pan,DateTime panDate,Action<int> read,Action<int> write){
  if(!Busy)return;
  if(phase==0){requested=utc;deadline=now+2000;nextRead=now+150;read(Completed);phase=1;return;}
  if(now>deadline){Busy=false;Status="Zero stopped at input "+(Completed+1)+": desk confirmation timed out. Check this channel.";return;}
  bool fresh=level.HasValue&&pan.HasValue&&levelDate>requested&&panDate>requested;
  if(phase==1&&fresh){write(Completed);phase=2;deadline=now+2000;nextRead=now+40;return;}
  if(phase==2&&now>=nextRead){requested=utc;read(Completed);phase=3;nextRead=now+150;return;}
  if(phase==3&&fresh&&level==Unity&&pan==0){Completed++;if(Completed==96){Busy=false;Status="Zero complete · all 96 input faders at 0 dB, pans centered.";}else{phase=0;Status="Zeroing inputs · "+Completed+"/96";}return;}
  if(phase!=2&&now>=nextRead){read(Completed);nextRead=now+150;}
 }
 public static void Test(){if(FaderProtocol.Label(Unity)!="0.00 dB")throw new Exception("Incorrect unity target");var operation=new ZeroInputOperation();int reads=0,writes=0;DateTime stamp=DateTime.UtcNow;for(int c=0;c<96;c++){int channel,value;byte[] packet=FaderWriteProtocol.Create(c,Unity,0);if(!FaderProtocol.TryRead(packet,out channel,out value)||channel!=c||value!=Unity)throw new Exception("Zero addressed wrong input");Action<int> read=delegate(int id){if(id!=c)throw new Exception("Wrong zero read target");reads++;};Action<int> write=delegate(int id){if(id!=c)throw new Exception("Wrong zero write target");writes++;};double t=c*100;operation.Tick(t,stamp,100,stamp,12,stamp,read,write);operation.Tick(t+1,stamp.AddMilliseconds(1),100,stamp,12,stamp,read,write);if(writes!=c)throw new Exception("Zero accepted stale data");operation.Tick(t+2,stamp.AddMilliseconds(2),100,stamp.AddMilliseconds(1),12,stamp.AddMilliseconds(1),read,write);operation.Tick(t+42,stamp.AddMilliseconds(42),Unity,stamp.AddMilliseconds(3),0,stamp.AddMilliseconds(3),read,write);operation.Tick(t+43,stamp.AddMilliseconds(43),Unity,stamp.AddMilliseconds(3),0,stamp.AddMilliseconds(3),read,write);if(operation.Completed!=c)throw new Exception("Zero accepted write echo as readback");operation.Tick(t+44,stamp.AddMilliseconds(44),Unity,stamp.AddMilliseconds(43),0,stamp.AddMilliseconds(43),read,write);stamp=stamp.AddMilliseconds(100);}if(operation.Busy||operation.Completed!=96||writes!=96||reads!=192)throw new Exception("Zero did not complete all inputs");var timeout=new ZeroInputOperation();timeout.Tick(0,stamp,null,stamp,null,stamp,delegate{},delegate{});timeout.Tick(2001,stamp,null,stamp,null,stamp,delegate{},delegate{});if(timeout.Busy)throw new Exception("Zero did not time out");var cancel=new ZeroInputOperation();cancel.Cancel();cancel.Tick(0,stamp,null,stamp,null,stamp,delegate{throw new Exception("Read after cancel");},delegate{throw new Exception("Write after cancel");});}
}
public sealed partial class FaderConnection {
 ZeroInputOperation zeroInputs;
 public bool ZeroInputsBusy{get{lock(gate)return zeroInputs!=null&&zeroInputs.Busy&&!stopping&&error==null;}}
 public string ZeroInputsStatus{get{lock(gate)return zeroInputs==null?"":zeroInputs.Status;}}
 public void StartZeroInputs(){lock(gate){if(stopping||error!=null||ZeroInputsBusy)throw new InvalidOperationException("Connect and wait for the current operation to finish.");Array.Clear(writes,0,96);Array.Clear(panWrites,0,96);zeroInputs=new ZeroInputOperation();}}
 public void CancelZeroInputs(){lock(gate)if(zeroInputs!=null)zeroInputs.Cancel();}
 void TickZeroInputs(FaderReader output,double now){if(zeroInputs==null||!zeroInputs.Busy||stopping)return;int c=zeroInputs.Completed;zeroInputs.Tick(now,DateTime.UtcNow,values[c],received[c],pans[c],panReceived[c],delegate(int id){output.Read(id,device);output.ReadPan(id,device);},delegate(int id){output.SetFader(id,ZeroInputOperation.Unity,device);output.SetPan(id,0,device);});}
}
sealed partial class Studio {
 void InstallZeroInputs(){var button=new Button{Text="Zero inputs",AutoSize=true};Style(button);toolbar.Controls.Add(button);var progress=new Label{AutoSize=true,Padding=new Padding(0,7,0,0)};toolbar.Controls.Add(progress);button.Click+=delegate{try{if(midi==null)return;if(midi.ZeroInputsBusy)midi.CancelZeroInputs();else midi.StartZeroInputs();}catch(Exception ex){state.Text=ex.Message;}};timer.Tick+=delegate{bool connected=midi!=null&&midi.Error==null;button.Enabled=connected;string text=connected&&midi.ZeroInputsBusy?"Stop zero":"Zero inputs";if(button.Text!=text)button.Text=text;string message=connected?midi.ZeroInputsStatus:"";if(progress.Text!=message)progress.Text=message;};}
}
