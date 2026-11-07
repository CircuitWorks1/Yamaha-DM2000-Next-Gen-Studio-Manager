using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

public static class StereoLink {
 static double[] db;
 static void Load(){if(db!=null)return;string[] lines=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"output-fader-db.txt"));db=new double[1024];db[0]=double.NegativeInfinity;for(int i=1;i<1024;i++)db[i]=double.Parse(lines[i].Replace(" dB",""),CultureInfo.InvariantCulture);}
 static int Nearest(double value){int n=Array.BinarySearch(db,value);if(n>=0)return n;n=~n;if(n<=1)return value<db[1]?0:1;if(n>=1024)return 1023;return value-db[n-1]<db[n]-value?n-1:n;}
 public static int[] AtLevel(int left,int right,int target){if(left<0||left>1023||right<0||right>1023||target<0||target>1023)throw new ArgumentOutOfRangeException();Load();if(target==0)return new int[]{0,0};if(left==0&&right==0)return new int[]{target,target};double lead=db[Math.Max(left,right)],delta=db[target]-lead;return new int[]{left==0?0:Nearest(db[left]+delta),right==0?0:Nearest(db[right]+delta)};}
 public static void Test(){for(int v=0;v<1024;v++){var pair=AtLevel(800,800,v);if(pair[0]!=v||pair[1]!=v)throw new Exception("Stereo unity linkage failed");}var p=AtLevel(900,800,950);Load();if(Math.Abs((db[p[0]]-db[p[1]])-(db[900]-db[800]))>.06)throw new Exception("Stereo balance changed");p=AtLevel(0,800,900);if(p[0]!=0||p[1]!=900)throw new Exception("Silent stereo leg was raised");p=AtLevel(800,700,0);if(p[0]!=0||p[1]!=0)throw new Exception("Stereo silence failed");}
}

public sealed partial class FaderConnection {
 public bool StereoLevels(out int left,out int right){left=right=0;lock(gate){Scalar l,r;var lp=MixerParameter.Level(124,-1);var rp=MixerParameter.Level(125,-1);if(!extra.TryGetValue(lp.Key,out l)||!extra.TryGetValue(rp.Key,out r)||(DateTime.UtcNow-l.Time).TotalSeconds>2||(DateTime.UtcNow-r.Time).TotalSeconds>2||Math.Abs((l.Time-r.Time).TotalMilliseconds)>40)return false;left=l.Value;right=r.Value;return true;}}
 public void SetStereoLevels(int left,int right){lock(gate){var l=MixerParameter.Level(124,-1);var r=MixerParameter.Level(125,-1);l.Write(left,device);r.Write(right,device);if(!ScalarValue(l,2).HasValue||!ScalarValue(r,2).HasValue||stopping||error!=null)throw new InvalidOperationException("Waiting for both stereo master channels.");Queue(l,left,2);Queue(r,right,2);}}
 public void SetStereoMute(bool muted){lock(gate){var l=MixerParameter.On(124);var r=MixerParameter.On(125);if(!ScalarValue(l,5).HasValue||!ScalarValue(r,5).HasValue||stopping||error!=null)throw new InvalidOperationException("Waiting for stereo master mute state.");Queue(l,muted?0:1,5);Queue(r,muted?0:1,5);}}
}

sealed partial class ChannelStrip {
 public bool IsStereoMaster;public int? RightMeter;
 void PaintStereoMeters(Graphics g){if(!IsStereoMaster)return;int h=Math.Max(60,Height-205);for(int side=0;side<2;side++){int x=Width-28+side*14;int? value=side==0?Meter:RightMeter;using(var track=new SolidBrush(Color.FromArgb(15,20,26)))g.FillRectangle(track,x,145,10,h);if(value.HasValue){double db=MeterProtocol.Decibels(value.Value);float fill=(float)(Math.Max(0,Math.Min(1,(db+96)/96))*h);using(var ink=new SolidBrush(value>=8191?Color.FromArgb(239,94,94):db>=-6?Color.FromArgb(232,192,91):Color.FromArgb(76,196,151)))g.FillRectangle(ink,x,145+h-fill,10,fill);}using(var ink=new SolidBrush(Color.FromArgb(160,177,193)))g.DrawString(side==0?"L":"R",Font,ink,x-1,127);}}
}

sealed partial class Studio {
 ChannelStrip stereoMaster;int stereoBasisL,stereoBasisR;bool stereoBasis;int? stereoOwnL,stereoOwnR;int? stereoTargetL,stereoTargetR;DateTime stereoSent;
 void InstallStereoMaster(){stereoMaster=new ChannelStrip(125){IsStereoMaster=true,OutputScale=true,DisplayTitle="Stereo master"};stereoMaster.ApplyMixer(new MixerChannelState{Id=124},-1);stereoMaster.DisplayTitle="Stereo master";stereoMaster.Pick+=delegate{channel=125;foreach(ChannelStrip strip in strips.Controls){strip.Selected=false;strip.Invalidate();}stereoMaster.Selected=true;stereoMaster.Invalidate();Details();};stereoMaster.FaderChanged+=delegate(int n,int raw){try{if(midi==null||!stereoBasis)throw new InvalidOperationException("Waiting for the stereo master.");var pair=StereoLink.AtLevel(stereoBasisL,stereoBasisR,raw);midi.SetStereoLevels(pair[0],pair[1]);stereoTargetL=pair[0];stereoTargetR=pair[1];stereoSent=DateTime.UtcNow;stereoOwnL=pair[0];stereoOwnR=pair[1];}catch(Exception e){stereoMaster.RejectEdit();state.Text=e.Message;}};stereoMaster.MuteEdited+=delegate(int n,bool muted){Act(delegate{midi.SetStereoMute(muted);});};masterPanel.Controls.Add(stereoMaster);}
 void UpdateStereoMaster(){var left=midi.Channel(124);var right=midi.Channel(125);int l,r;bool coherent=midi.StereoLevels(out l,out r);if(coherent){bool pending=stereoTargetL.HasValue;if(pending&&((stereoTargetL==l&&stereoTargetR==r)||(DateTime.UtcNow-stereoSent).TotalSeconds>1)){stereoTargetL=stereoTargetR=null;pending=false;}if(!stereoMaster.Dragging&&!pending&&!(stereoOwnL==l&&stereoOwnR==r)&&(l>0||r>0||!stereoBasis)){stereoBasisL=l;stereoBasisR=r;stereoBasis=true;}stereoMaster.Raw=Math.Max(l,r);}else if(!left.Level.HasValue||!right.Level.HasValue)stereoMaster.Raw=null;var display=new MixerChannelState{Id=124,Name=left.Name,On=left.On.HasValue&&right.On.HasValue?(int?)(left.On==0&&right.On==0?0:1):null};stereoMaster.ApplyMixer(display,-1);stereoMaster.DisplayTitle="Stereo master";stereoMaster.Meter=left.Meter;stereoMaster.RightMeter=right.Meter;stereoMaster.MeterPoint=MeterProtocol.PointLabel(outputPoint);stereoMaster.RefreshEdit();stereoMaster.Invalidate();}
 void ResetStereoMaster(){stereoBasis=false;stereoTargetL=stereoTargetR=null;stereoOwnL=stereoOwnR=null;if(stereoMaster!=null){stereoMaster.RejectEdit();stereoMaster.Raw=stereoMaster.Meter=stereoMaster.RightMeter=null;stereoMaster.ApplyMixer(new MixerChannelState{Id=124},-1);stereoMaster.DisplayTitle="Stereo master";stereoMaster.Invalidate();}}
}



