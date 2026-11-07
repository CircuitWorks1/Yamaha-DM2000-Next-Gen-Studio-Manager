using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public static class FaderProtocol {
 // Yamaha DM2000 V2 Parameter Change List, kInputFader / kFader.
 public static byte[] Request(int channel,int device){if(channel<0||channel>95||device<0||device>15)throw new ArgumentOutOfRangeException();return new byte[]{0xF0,0x43,(byte)(0x30|device),0x3E,0x7F,1,0x1C,0,(byte)channel,0xF7};}
 public static bool TryRead(byte[] p,out int channel,out int raw){channel=raw=0;if(p==null||p.Length!=14||p[0]!=0xF0||p[1]!=0x43||(p[2]&0xF0)!=0x10||p[3]!=0x3E||p[4]!=0x7F||p[5]!=1||p[6]!=0x1C||p[7]!=0||p[8]>95||p[13]!=0xF7)return false;for(int i=2;i<13;i++)if(p[i]>127)return false;int value=(p[9]<<21)|(p[10]<<14)|(p[11]<<7)|p[12];if(value>1023)return false;channel=p[8];raw=value;return true;}
 static string[] labels;
 public static string Label(int raw){if(raw<0||raw>1023)return "Unknown";if(labels==null){labels=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fader-db.txt"));if(labels.Length!=1024)throw new InvalidDataException("Fader table must contain 1024 values");}return labels[raw];}
 public static void Test(){int ch,v;for(int c=0;c<96;c++)for(int r=0;r<1024;r++){byte[] p={0xF0,0x43,0x10,0x3E,0x7F,1,0x1C,0,(byte)c,0,0,(byte)(r>>7),(byte)(r&127),0xF7};if(!TryRead(p,out ch,out v)||ch!=c||v!=r)throw new Exception("Fader decode failed");}byte[] req=Request(95,15);if(req.Length!=10||req[2]!=0x3F||req[8]!=95||TryRead(req,out ch,out v))throw new Exception("Request validation failed");byte[] bad={0xF0,0x43,0x10,0x3E,0x7F,1,0x1C,1,0,0,0,0,0,0xF7};if(TryRead(bad,out ch,out v))throw new Exception("Fade time must not decode as fader");bad[7]=0;bad[8]=96;if(TryRead(bad,out ch,out v))throw new Exception("Invalid channel accepted");bad[8]=0;bad[11]=8;if(TryRead(bad,out ch,out v))throw new Exception("Out of range value accepted");if(Label(0)!="-Inf dB"||Label(823)!="0.00 dB"||Label(1023)!="10.00 dB")throw new Exception("Fader table landmarks invalid");}
}

// Exposes validated fader writes and fixed read/subscription messages; no arbitrary messages or scene operations.
public sealed class FaderReader : IDisposable {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Caps{public ushort manufacturer,product;public uint version;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string name;public ushort technology,voices,notes,channelMask;public uint support;}
 [StructLayout(LayoutKind.Sequential)]struct Header{public IntPtr data;public uint length,recorded;public UIntPtr user;public uint flags;public IntPtr next;public UIntPtr reserved;public uint offset;[MarshalAs(UnmanagedType.ByValArray,SizeConst=8)]public UIntPtr[] padding;}
 [DllImport("winmm.dll")]static extern uint midiOutGetNumDevs();
 [DllImport("winmm.dll",CharSet=CharSet.Unicode)]static extern uint midiOutGetDevCapsW(UIntPtr id,out Caps caps,uint size);
 [DllImport("winmm.dll")]static extern uint midiOutOpen(out IntPtr h,uint id,IntPtr cb,IntPtr instance,uint flags);
 [DllImport("winmm.dll")]static extern uint midiOutPrepareHeader(IntPtr h,IntPtr p,uint size);
 [DllImport("winmm.dll")]static extern uint midiOutLongMsg(IntPtr h,IntPtr p,uint size);
 [DllImport("winmm.dll")]static extern uint midiOutUnprepareHeader(IntPtr h,IntPtr p,uint size);
 [DllImport("winmm.dll")]static extern uint midiOutReset(IntPtr h);
 [DllImport("winmm.dll")]static extern uint midiOutClose(IntPtr h);
 IntPtr handle;readonly List<IntPtr> pending=new List<IntPtr>();static readonly uint Size=(uint)Marshal.SizeOf(typeof(Header));
 public static SortedDictionary<uint,string> Devices(){var d=new SortedDictionary<uint,string>();for(uint i=0;i<midiOutGetNumDevs();i++){Caps c;if(midiOutGetDevCapsW((UIntPtr)i,out c,(uint)Marshal.SizeOf(typeof(Caps)))==0)d[i]=c.name;}return d;}
 static void Check(uint n){if(n!=0)throw new InvalidOperationException("MIDI read-request output error "+n);}
 public FaderReader(uint id){Check(midiOutOpen(out handle,id,IntPtr.Zero,IntPtr.Zero,0));}
 public void ReadParameter(MixerParameter p,int device){Send(p.Request(device));}public void SetParameter(MixerParameter p,int value,int device){Send(p.Write(value,device));}public void SubscribeOutputMeters(int group,int count,int device){if(group<1||group>4||count<1||count>12)throw new ArgumentOutOfRangeException();Send(new byte[]{240,67,(byte)(48|device),62,6,33,(byte)group,0,127,0,(byte)count,247});}public void ReadPan(int channel,int device){Send(PanProtocol.Request(channel,device));}public void SetPan(int channel,int value,int device){Send(PanProtocol.Create(channel,value,device));}public void SetFader(int channel,int raw,int device){Send(FaderWriteProtocol.Create(channel,raw,device));} public void Read(int channel,int device){Send(FaderProtocol.Request(channel,device));} public void ReadMeterPoint(bool output,int device){Send(new byte[]{0xF0,0x43,(byte)(0x30|device),0x3E,6,3,0x0C,(byte)(output?1:0),0,0xF7});} public void SubscribeMeters(int device){Send(new byte[]{0xF0,0x43,(byte)(0x30|device),0x3E,6,0x21,0,0,0x7F,0,96,0xF7});} void Send(byte[] bytes){Reap();if(pending.Count>=128)throw new IOException("MIDI requests are not completing; disconnect and check USB.");IntPtr data=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,data,bytes.Length);IntPtr p=Marshal.AllocHGlobal((int)Size);Marshal.StructureToPtr(new Header{data=data,length=(uint)bytes.Length,padding=new UIntPtr[8]},p,false);uint r=midiOutPrepareHeader(handle,p,Size);if(r!=0){Marshal.FreeHGlobal(data);Marshal.FreeHGlobal(p);Check(r);}pending.Add(p);Check(midiOutLongMsg(handle,p,Size));}
 void Reap(){for(int i=pending.Count-1;i>=0;i--){var h=(Header)Marshal.PtrToStructure(pending[i],typeof(Header));if((h.flags&1)==0)continue;Check(midiOutUnprepareHeader(handle,pending[i],Size));Marshal.FreeHGlobal(h.data);Marshal.FreeHGlobal(pending[i]);pending.RemoveAt(i);}}
 public void Dispose(){if(handle==IntPtr.Zero)return;midiOutReset(handle);Reap();if(pending.Count==0){midiOutClose(handle);handle=IntPtr.Zero;}}
 public static void Probe(){var inputs=new List<MidiInput>();var names=new List<string>();string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fader-report.txt");using(var w=new StreamWriter(file)){try{foreach(var p in MidiInput.Devices()){if(!p.Value.Contains("DM2000"))continue;try{inputs.Add(new MidiInput(p.Key));names.Add(p.Value);w.WriteLine("Listening: "+p.Value);}catch(Exception e){w.WriteLine(p.Value+": "+e.Message);}}bool found=false;foreach(var p in Devices()){if(!p.Value.Contains("DM2000"))continue;using(var output=new FaderReader(p.Key)){for(int dev=0;dev<16;dev++){output.Read(0,dev);for(int wait=0;wait<3;wait++){Thread.Sleep(30);for(int i=0;i<inputs.Count;i++){inputs[i].Pump();byte[] packet;while(inputs[i].Packets.TryDequeue(out packet)){int ch,v;if(FaderProtocol.TryRead(packet,out ch,out v)){w.WriteLine("FADER REPLY: output="+p.Value+" input="+names[i]+" device="+((packet[2]&15)+1)+" channel="+(ch+1)+" raw="+v+" level="+FaderProtocol.Label(v));File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"connection.ini"),new string[]{names[i],p.Value,(packet[2]&15).ToString()});found=true;}}}}if(found)break;}}if(found)break;}w.WriteLine(found?"Verified fader-read reply received.":"No fader reply. Check console Studio Manager port and MIDI Parameter Change RX settings.");}finally{foreach(var input in inputs)input.Dispose();}}}
}




