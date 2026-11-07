using System;
using System.Drawing;
using System.Windows.Forms;

sealed partial class Studio {
 FlowLayoutPanel inspectorIdentity,inspectorControls,inspectorRouting;bool sizingSource;
 void FitSourceField(){if(sizingSource||sourceValue.Parent==null)return;sizingSource=true;try{int width=Math.Max(40,sourceValue.ClientSize.Width);int textHeight=TextRenderer.MeasureText(sourceValue.Text,sourceValue.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;int height=Math.Max(54,23+textHeight+8);if(sourceValue.Parent.Height!=height)sourceValue.Parent.Height=height;}finally{sizingSource=false;}}
 FlowLayoutPanel InspectorColumn(){return new FlowLayoutPanel{Width=228,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,16,0),Padding=Padding.Empty};}
 void ArrangeInspector(){
  Control source=sourceValue.Parent,level=faderValue.Parent,signal=signalValue.Parent,inputPointField=inputValue.Parent,outputPointField=outputValue.Parent,names=channelName.Parent.Parent;
  info.SuspendLayout();info.Controls.Clear();
  inspectorIdentity=InspectorColumn();inspectorControls=InspectorColumn();inspectorRouting=InspectorColumn();
  channelTitle.Height=30;source.Height=54;names.Height=82;sourceValue.TextChanged+=delegate{FitSourceField();};sourceValue.SizeChanged+=delegate{FitSourceField();};
  foreach(Control c in new Control[]{channelTitle,names,source,cardField}){c.Margin=new Padding(0,0,0,8);inspectorIdentity.Controls.Add(c);}
  var levels=new TableLayoutPanel{Width=228,Height=54,ColumnCount=2,RowCount=1,Margin=new Padding(0,0,0,8)};
  levels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));levels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
  level.Dock=signal.Dock=DockStyle.Fill;level.Margin=signal.Margin=Padding.Empty;faderValue.Font=signalValue.Font=new Font("Segoe UI",11);
  levels.Controls.Add(level,0,0);levels.Controls.Add(signal,1,0);
  inputPointField.Height=outputPointField.Height=48;
  foreach(Control c in new Control[]{levels,inspectorPanField,inputPointField,outputPointField}){c.Margin=new Padding(0,0,0,8);inspectorControls.Controls.Add(c);}
  destinationField.Margin=Padding.Empty;inspectorRouting.Controls.Add(destinationField);
  info.Controls.AddRange(new Control[]{inspectorIdentity,inspectorControls,inspectorRouting});info.ResumeLayout(true);
 }
 void ResizeInspectorExtras(){if(inspectorIdentity==null)return;int available=info.MaximumSize.Width;bool wide=available>=780;info.FlowDirection=wide?FlowDirection.LeftToRight:FlowDirection.TopDown;info.WrapContents=false;
  int width=wide?(available-32)/3:available;
  foreach(var column in new FlowLayoutPanel[]{inspectorIdentity,inspectorControls,inspectorRouting}){column.Width=width;column.MaximumSize=new Size(width,0);column.Margin=new Padding(0,0,wide?16:0,wide?0:8);foreach(Control field in column.Controls)field.Width=width;}
  inspectorRouting.Margin=Padding.Empty;FitSourceField();
 }
}
