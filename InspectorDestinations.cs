using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

sealed partial class Studio {
 Panel destinationField;Button[] destinationButtons=new Button[11];Label destinationStatus;FaderConnection destinationConnection;int destinationChannel=-1;
 void InstallDestinationInspector(){
  destinationField=new Panel{Width=228,Height=242,Margin=new Padding(0,0,12,5)};
  var title=new Label{Text="Channel destinations",Dock=DockStyle.Top,Height=26,ForeColor=Color.FromArgb(160,177,193)};
  var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=6,Padding=new Padding(0,3,0,3)};
  grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
  for(int row=0;row<6;row++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/6));
  string[] labels={"Stereo","Follow pan","Direct out","Bus 1","Bus 2","Bus 3","Bus 4","Bus 5","Bus 6","Bus 7","Bus 8"};
  for(int i=0;i<11;i++){int index=i;var button=new Button{Text=labels[i],Dock=DockStyle.Fill,Enabled=false,Margin=new Padding(2),FlatStyle=FlatStyle.Flat};destinationButtons[i]=button;grid.Controls.Add(button,i%2,i/2);button.Click+=delegate{ToggleDestination(index);};}
  var read=new Button{Text="Read",Dock=DockStyle.Fill,Margin=new Padding(2),FlatStyle=FlatStyle.Flat};grid.Controls.Add(read,1,5);read.Click+=delegate{ReadInspectorDestinations();};
  destinationStatus=new Label{Text="Waiting for console data",Dock=DockStyle.Bottom,Height=36,AutoEllipsis=true};
  destinationField.Controls.Add(grid);destinationField.Controls.Add(title);destinationField.Controls.Add(destinationStatus);info.Controls.Add(destinationField);
 }
 void ReadInspectorDestinations(){if(midi==null||SelectedId>=96)return;var parameters=new List<MixerParameter>();for(int i=0;i<11;i++)parameters.Add(MixerParameter.Route(SelectedId,i));midi.ReadRouting(parameters);}
 void ToggleDestination(int index){try{if(midi==null||SelectedId>=96)return;var p=MixerParameter.Route(SelectedId,index);int? value=midi.RoutingValue(p);if(!value.HasValue)throw new InvalidOperationException("Wait for this destination to load from the desk.");midi.WriteRouting(p,value.Value,1-value.Value);RefreshDestinationInspector();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Channel destinations",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
 void RefreshDestinationInspector(){if(destinationField==null)return;destinationField.Visible=SelectedId<96;if(destinationConnection!=midi||destinationChannel!=SelectedId){destinationConnection=midi;destinationChannel=SelectedId;ReadInspectorDestinations();}bool connected=midi!=null&&midi.Error==null;int loaded=0;for(int i=0;i<11;i++){int? value=connected&&SelectedId<96?midi.RoutingValue(MixerParameter.Route(SelectedId,i)):null;var button=destinationButtons[i];button.Enabled=value.HasValue&&!midi.RoutingBusy;if(value.HasValue)loaded++;Color color=value==1?Color.FromArgb(32,116,134):Color.FromArgb(38,47,58);if(button.BackColor!=color)button.BackColor=color;button.AccessibleDescription=value==1?"Connected":value==0?"Disconnected":"Waiting for desk";}string text=!connected?"Connect to read destinations":loaded<11?"Reading destinations…":midi.RoutingBusy?"Confirming with desk…":string.IsNullOrEmpty(midi.RoutingStatus)?"Highlighted destinations are connected":midi.RoutingStatus;if(destinationStatus.Text!=text)destinationStatus.Text=text;}
}
