using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MR18Midi {
static class Theme {
 public static readonly Color Bg=Color.FromArgb(17,22,27),Panel=Color.FromArgb(25,32,39),Border=Color.FromArgb(49,61,72),Text=Color.FromArgb(229,236,241),Muted=Color.FromArgb(151,168,181),Cyan=Color.FromArgb(23,194,229),Amber=Color.FromArgb(244,186,64),Green=Color.FromArgb(45,197,117),Red=Color.FromArgb(235,76,86);
 public static Button Button(string text,Action action,bool primary=false){var b=new Button{Text=text,AutoSize=true,Height=36,Padding=new Padding(10,3,10,3),FlatStyle=FlatStyle.Flat,BackColor=primary?Cyan:Panel,ForeColor=primary?Bg:Text,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=primary?Cyan:Border;b.Click+=(s,e)=>action();return b;}
 public static Label Label(string text,int size=10,bool bold=false){return new Label{Text=text,AutoSize=true,ForeColor=Text,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(4,8,4,8)};}
 public static void Style(Control c){if(!(c is Button))c.ForeColor=Text;if(c is TextBox||c is ComboBox||c is NumericUpDown){c.BackColor=Panel;if(c is TextBox)((TextBox)c).BorderStyle=BorderStyle.FixedSingle;}foreach(Control child in c.Controls)Style(child);}
}
public partial class MainForm {
 Panel mixerPage,controllerPage;Button mixerTab,controllerTab,viewFirst,viewSecond;
 Label footer,profileLabel,connectionLabel,inspectorTitle,learnStatus;FlowLayoutPanel connectionBar;
 MixerStrip[] strips=new MixerStrip[9];int viewStart;bool controllersView,loadingInspector;
 DateTime nextVisual=DateTime.MinValue;string shownProfile;
 TextBox editName;ComboBox editKind,editTarget,editMode;NumericUpDown editBank,editMidi,editNumber,editChannel,editMin,editMax,editStep;CheckBox editEnabled;
 class Choice {public string Key{get;set;}public string Title{get;set;}}
 ComboBox Choices(params string[] pairs){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat};var items=new List<Choice>();for(int i=0;i<pairs.Length;i+=2)items.Add(new Choice{Key=pairs[i],Title=pairs[i+1]});c.DisplayMember="Title";c.ValueMember="Key";c.DataSource=items;return c;}
 NumericUpDown Number(decimal min,decimal max,int decimals=0){return new NumericUpDown{Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals==0?1:.5m,Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle};}
 void BuildInterface(){
  SetupChrome();BackColor=Theme.Bg;ForeColor=Theme.Text;DoubleBuffered=true;
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(18,6,18,8),BackColor=Theme.Bg};
  foreach(int h in new[]{56,58,48})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(root);
  root.Controls.Add(BuildHeader(),0,0);
  connectionBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,BackColor=Theme.Panel,Padding=new Padding(8)};
  connectionBar.Controls.Add(Theme.Label("MR18 IP"));host.Width=130;host.Margin=new Padding(5,4,8,0);connectionBar.Controls.Add(host);connect.Height=34;StyleButton(connect);connectionBar.Controls.Add(connect);
  connectionLabel=Theme.Label(L.T("● Нет соединения"));connectionLabel.Margin=new Padding(14,7,10,0);connectionBar.Controls.Add(connectionLabel);
  armed.Appearance=Appearance.Button;armed.FlatStyle=FlatStyle.Flat;armed.FlatAppearance.BorderColor=Theme.Amber;armed.Padding=new Padding(10,4,10,4);armed.Text=L.T("ТЕСТОВЫЙ РЕЖИМ");armed.AutoSize=false;armed.Width=245;armed.Height=34;connectionBar.Controls.Add(armed);root.Controls.Add(connectionBar,0,1);
  var banks=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,5,0,0)};
  banks.Controls.Add(Theme.Label(L.T("Каналы"),10,true));viewFirst=Theme.Button("1–8",()=>{viewStart=0;bank.Value=1;RefreshVisuals(true);});viewSecond=Theme.Button("9–16",()=>{viewStart=8;bank.Value=2;RefreshVisuals(true);});banks.Controls.Add(viewFirst);banks.Controls.Add(viewSecond);banks.Controls.Add(Theme.Label(L.T("Банк MIDI")));bank.Margin=new Padding(6,6,12,0);banks.Controls.Add(bank);
  profileLabel=Theme.Label(L.T("Профиль не сохранён"));banks.Controls.Add(profileLabel);banks.Controls.Add(Theme.Button(L.T("Сохранить"),()=>Guard(SaveProfile)));root.Controls.Add(banks,0,2);
  var pages=new Panel{Dock=DockStyle.Fill};root.Controls.Add(pages,0,3);
  mixerPage=new Panel{Dock=DockStyle.Fill};controllerPage=new Panel{Dock=DockStyle.Fill,Visible=false};pages.Controls.Add(mixerPage);pages.Controls.Add(controllerPage);
  var channels=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=9,RowCount=1,Padding=new Padding(0,8,0,0)};
  for(int i=0;i<9;i++){
   channels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,i==8?12:11));int index=i;
   var strip=new MixerStrip{Dock=DockStyle.Fill,Margin=new Padding(0,0,i==7?15:8,0),Main=i==8};strips[i]=strip;
   strip.LevelChanged+=v=>Guard(()=>DirectLevel(index,v));strip.SwitchClicked+=target=>Guard(()=>DirectSwitch(index,target));strip.GainClicked+=()=>Guard(()=>EditGain(index));channels.Controls.Add(strip,i,0);
  }mixerPage.Controls.Add(channels);
  BuildControllers();footer=Theme.Label(L.T("ТЕСТ · изменения микшеру не отправляются"),9);footer.Dock=DockStyle.Fill;root.Controls.Add(footer,0,4);
  Theme.Style(root);grid.DataSource=mappings;grid.SelectionChanged+=(s,e)=>LoadInspector();
  mappings.ListChanged+=(s,e)=>{if(!loadingInspector)RefreshVisuals(true);};
 }
 void StyleButton(Button b){b.FlatStyle=FlatStyle.Flat;b.BackColor=Theme.Panel;b.ForeColor=Theme.Text;b.FlatAppearance.BorderColor=Theme.Border;b.Padding=new Padding(6,2,6,2);}
 void ShowPage(bool controllers){controllersView=controllers;if(controllers)Disarm();mixerPage.Visible=!controllers;controllerPage.Visible=controllers;mixerTab.BackColor=controllers?Theme.Panel:Theme.Cyan;mixerTab.ForeColor=controllers?Theme.Text:Theme.Bg;controllerTab.BackColor=controllers?Theme.Cyan:Theme.Panel;controllerTab.ForeColor=controllers?Theme.Bg:Theme.Text;RefreshVisuals(true);}
 void BuildControllers(){
  var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(0,8,0,0)};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,250));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,350));controllerPage.Controls.Add(columns);
  var sidebar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Panel,Padding=new Padding(14),Margin=new Padding(0,0,12,0)};
  sidebar.Controls.Add(Theme.Label(L.T("Устройство"),16,true));devices.Width=205;devices.FlatStyle=FlatStyle.Flat;sidebar.Controls.Add(devices);sidebar.Controls.Add(Theme.Button(L.T("Обновить список"),()=>Guard(RefreshDevices)));midiButton.Height=38;StyleButton(midiButton);sidebar.Controls.Add(midiButton);
  var desc=Theme.Label(L.T("Один MIDI-вход одновременно.\nSMC: USB / обычный MIDI.\nPitch поддерживает 14-битные\nфейдеры Mackie."),9);desc.MaximumSize=new Size(210,100);desc.ForeColor=Theme.Muted;sidebar.Controls.Add(desc);
  sidebar.Controls.Add(Theme.Label(L.T("Профили"),16,true));sidebar.Controls.Add(Theme.Button(L.T("Загрузить XML…"),()=>Guard(()=>{LoadProfile();LoadInspector();RefreshVisuals(true);})));sidebar.Controls.Add(Theme.Button(L.T("Сохранить как…"),()=>Guard(SaveProfile)));sidebar.Controls.Add(Theme.Button(L.T("Пример 8 каналов"),()=>Guard(()=>{Example();LoadInspector();})));
  var hint=Theme.Label(L.T("Пример содержит условные\nномера MIDI. Назначьте свои\nконтролы через обучение."),9);hint.ForeColor=Theme.Muted;sidebar.Controls.Add(hint);columns.Controls.Add(sidebar,0,0);
  var middle=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=new Padding(0,0,12,0)};middle.RowStyles.Add(new RowStyle(SizeType.Absolute,48));middle.RowStyles.Add(new RowStyle(SizeType.Percent,100));middle.RowStyles.Add(new RowStyle(SizeType.Absolute,34));middle.RowStyles.Add(new RowStyle(SizeType.Absolute,128));
  var tools=new FlowLayoutPanel{Dock=DockStyle.Fill};tools.Controls.Add(Theme.Label(L.T("Назначения"),16,true));tools.Controls.Add(Theme.Button(L.T("+ Добавить"),()=>Guard(()=>{AddMapping();LoadInspector();})));tools.Controls.Add(Theme.Button(L.T("Удалить"),()=>Guard(Delete)));middle.Controls.Add(tools,0,0);
  grid.Columns.Clear();grid.ReadOnly=true;grid.AllowUserToResizeRows=false;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.GridColor=Theme.Border;grid.BackgroundColor=Theme.Panel;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersHeight=42;grid.RowTemplate.Height=50;
  grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Panel,ForeColor=Theme.Muted,Font=new Font("Segoe UI",10),SelectionBackColor=Theme.Panel};
  grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Panel,ForeColor=Theme.Text,SelectionBackColor=Color.FromArgb(19,69,84),SelectionForeColor=Theme.Text,Padding=new Padding(7,0,0,0)};
  ColText("Name",L.T("Контрол"),100);ColText("Destination",L.T("Назначение"),100);ColText("MidiLabel","MIDI",100);middle.Controls.Add(grid,0,1);
  midiStatus.AutoSize=false;midiStatus.Dock=DockStyle.Fill;midiStatus.ForeColor=Theme.Cyan;middle.Controls.Add(midiStatus,0,2);
  log.BackColor=Theme.Bg;log.ForeColor=Theme.Muted;log.BorderStyle=BorderStyle.FixedSingle;middle.Controls.Add(log,0,3);columns.Controls.Add(middle,1,0);
  var right=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel,Padding=new Padding(14)};columns.Controls.Add(right,2,0);
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};right.Controls.Add(scroll);
  var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,118));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(fields);
  inspectorTitle=Theme.Label(L.T("Назначение"),16,true);AddField(fields,"",inspectorTitle);
  var learnButton=Theme.Button(L.T("Обучить MIDI"),()=>Guard(()=>{if(ApplyInspector(false))Learn();}),true);learnButton.Dock=DockStyle.Fill;AddField(fields,"",learnButton);
  learnStatus=Theme.Label(L.T("Подвигайте контрол после\nнажатия «Обучить MIDI»."),9);learnStatus.MaximumSize=new Size(185,60);AddField(fields,"MIDI Learn",learnStatus);
  editName=new TextBox{Dock=DockStyle.Fill};AddField(fields,L.T("Название"),editName);
  editEnabled=new CheckBox{Text=L.T("Включено"),Checked=true,AutoSize=true};AddField(fields,L.T("Назначение"),editEnabled);
  editTarget=Choices("Fader",L.T("Уровень канала"),"Gain",L.T("Gain физ. входа"),"Mute","Mute","Solo","Solo","Main","Main LR","Bank+",L.T("Следующий банк"),"Bank-",L.T("Предыдущий банк"));AddField(fields,L.T("Управляет"),editTarget);
  editChannel=Number(1,16);AddField(fields,L.T("Канал / вход"),editChannel);
  editMode=Choices("Pickup",L.T("Подхват уровня"),"Absolute",L.T("Абсолютный"),"Relative2",L.T("Энкодер ± (2)"),"RelativeOffset",L.T("Энкодер центр 64"),"RelativeSign",L.T("Энкодер ± (Sign)"),"Toggle",L.T("Переключение"),"Gate",L.T("Пока нажата"),"Increase",L.T("Прибавить"),"Decrease",L.T("Убавить"));AddField(fields,L.T("Режим"),editMode);
  editMin=Number(-90,60,1);editMax=Number(-90,60,1);editStep=Number(.1m,12,1);AddField(fields,L.T("От, dB"),editMin);AddField(fields,L.T("До, dB"),editMax);AddField(fields,L.T("Шаг, dB"),editStep);
  editKind=Choices("CC","CC","Note","Note","Pitch","Pitch Bend");AddField(fields,L.T("Тип MIDI"),editKind);
  editMidi=Number(1,16);editNumber=Number(0,127);editBank=Number(0,8);AddField(fields,L.T("MIDI-канал"),editMidi);AddField(fields,L.T("Номер"),editNumber);AddField(fields,L.T("Банк (0=все)"),editBank);
  var apply=Theme.Button(L.T("Применить"),()=>Guard(()=>ApplyInspector(true)),true);apply.Dock=DockStyle.Fill;AddField(fields,"",apply);
  AddField(fields,"",Theme.Button(L.T("Отменить правки"),LoadInspector));
  editTarget.SelectedValueChanged+=(s,e)=>{if(loadingInspector)return;string target=Convert.ToString(editTarget.SelectedValue);if(target=="Gain"){editMin.Value=-12;editMax.Value=30;editMode.SelectedValue="Pickup";}else if(target=="Mute"||target=="Solo")editMode.SelectedValue="Toggle";else if(target=="Fader"||target=="Main"){editMin.Value=-90;editMax.Value=0;editMode.SelectedValue="Pickup";}};
 }
 void AddField(TableLayoutPanel t,string label,Control control){int row=t.RowCount++;t.RowStyles.Add(new RowStyle(SizeType.AutoSize));var l=Theme.Label(label,9);l.ForeColor=Theme.Muted;l.Margin=new Padding(0,10,6,4);control.Margin=new Padding(0,5,0,5);t.Controls.Add(l,0,row);t.Controls.Add(control,1,row);}
 void LoadInspector(){if(editName==null)return;var m=Selected();if(m==null)return;loadingInspector=true;try{inspectorTitle.Text=m.Name;editName.Text=m.Name;editEnabled.Checked=m.Enabled;editTarget.SelectedValue=m.Target;editMode.SelectedValue=m.Mode;editKind.SelectedValue=m.Kind;editChannel.Value=m.Channel;editBank.Value=m.Bank;editMidi.Value=m.MidiChannel;editNumber.Value=m.Number;editMin.Value=(decimal)m.Min;editMax.Value=(decimal)m.Max;editStep.Value=(decimal)m.Step;learnStatus.Text=m.MidiLabel;}finally{loadingInspector=false;}}
 bool ApplyInspector(bool report){
  var m=Selected();if(m==null)return false;
  var candidate=new Mapping{Name=editName.Text.Trim(),Enabled=editEnabled.Checked,Target=Convert.ToString(editTarget.SelectedValue),Mode=Convert.ToString(editMode.SelectedValue),Kind=Convert.ToString(editKind.SelectedValue),Channel=(int)editChannel.Value,Bank=(int)editBank.Value,MidiChannel=(int)editMidi.Value,Number=(int)editNumber.Value,Min=(double)editMin.Value,Max=(double)editMax.Value,Step=(double)editStep.Value};
  string error=candidate.Validate();if(error!=null){MessageBox.Show(this,error,L.T("Проверьте назначение"),MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
  Disarm();m.Name=candidate.Name;m.Enabled=candidate.Enabled;m.Target=candidate.Target;m.Mode=candidate.Mode;m.Kind=candidate.Kind;m.Channel=candidate.Channel;m.Bank=candidate.Bank;m.MidiChannel=candidate.MidiChannel;m.Number=candidate.Number;m.Min=candidate.Min;m.Max=candidate.Max;m.Step=candidate.Step;mappings.ResetBindings();engine.Reset();LoadInspector();if(report)Write(L.T("Назначение применено: ")+m.Name);return true;
 }
 IEnumerable<string> DisplayPaths(){
  // Read actual values for all strips. No synthetic audio meters.
  for(int ch=1;ch<=16;ch++){yield return "/ch/"+ch.ToString("00")+"/mix/fader";yield return "/ch/"+ch.ToString("00")+"/mix/on";yield return "/-stat/solosw/"+ch.ToString("00");yield return "/headamp/"+ch.ToString("00")+"/gain";yield return "/ch/"+ch.ToString("00")+"/config/name";}yield return "/lr/mix/fader";
 }
 double? VisibleValue(string path){double v;if(!armed.Checked){State s;if(simulation.TryGetValue(path,out s))return s.Value;}return mixer.Fresh(path,out v)?(double?)v:null;}
 void DirectWrite(string path,object value,bool discrete){
  if(controllersView)return;
  if(mixer.Model!=null&&!mixer.Model.Supports(path))return;
  if(armed.Checked){if(!mixer.Live){Disarm();return;}commands.Add(path,value,discrete);mixer.Values[path]=new State{Value=Convert.ToDouble(value),Time=DateTime.UtcNow};}
  else simulation[path]=new State{Value=Convert.ToDouble(value),Time=DateTime.UtcNow};
  continuousLog[path]=(armed.Checked?"LIVE ":"TEST ")+path+" → "+Convert.ToString(value,CultureInfo.InvariantCulture);RefreshVisuals(true);
 }
 void DirectLevel(int index,double value){int ch=viewStart+index+1;string path=index==8?"/lr/mix/fader":"/ch/"+ch.ToString("00")+"/mix/fader";DirectWrite(path,(float)Osc.Clamp(value,0,1),false);}
 void DirectSwitch(int index,string target){if(index==8)return;int ch=viewStart+index+1;string path=target=="Mute"?"/ch/"+ch.ToString("00")+"/mix/on":"/-stat/solosw/"+ch.ToString("00");double? v=VisibleValue(path);if(!v.HasValue&&armed.Checked){mixer.Send(path);Write(L.T("Запрошено состояние кнопки; повторите после ответа MR18."));return;}double current=v??(target=="Mute"?1:0);DirectWrite(path,current>=.5?0:1,true);}
 void EditGain(int index){if(index==8)return;int input=viewStart+index+1;if(mixer.Model!=null&&input>mixer.Model.Preamps){Write("This input has no supported analog preamp gain.");return;}string path="/headamp/"+input.ToString("00")+"/gain";double? v=VisibleValue(path);if(armed.Checked&&!v.HasValue){mixer.Send(path);Write(L.T("Ожидается Gain входа ")+input+L.T("; повторите после ответа."));return;}
  using(var dialog=new Form{Text=L.T("Gain — физический вход ")+input,Size=new Size(420,220),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Theme.Bg,ForeColor=Theme.Text}){
   var p=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),FlowDirection=FlowDirection.TopDown};dialog.Controls.Add(p);p.Controls.Add(Theme.Label(L.T("Аналоговый вход ")+input+L.T(" · от −12 до +60 dB"),11,true));var n=Number(-12,60,1);n.Width=160;n.Value=(decimal)Osc.Clamp(v.HasValue?-12+72*v.Value:0,-12,60);p.Controls.Add(n);var b=Theme.Button(armed.Checked?L.T("Применить к MR18"):L.T("Применить в тесте"),()=>dialog.DialogResult=DialogResult.OK,true);p.Controls.Add(b);Theme.Style(p);if(dialog.ShowDialog(this)==DialogResult.OK)DirectWrite(path,(float)(((double)n.Value+12)/72),false);
  }
 }
 void RefreshVisuals(bool force){if(footer==null)return;DateTime now=DateTime.UtcNow;if(!force&&now<nextVisual)return;nextVisual=now.AddMilliseconds(40);
  connectionLabel.Text=mixer.Live?"● "+mixer.Model.Name+" connected":L.T("● Нет связи с MR18");connectionLabel.ForeColor=mixer.Live?Theme.Green:Theme.Muted;
  armed.Text=armed.Checked?L.T("УПРАВЛЕНИЕ ВКЛЮЧЕНО"):L.T("ТЕСТОВЫЙ РЕЖИМ");armed.BackColor=armed.Checked?Color.FromArgb(15,94,57):Theme.Panel;armed.ForeColor=armed.Checked?Theme.Text:Theme.Amber;
  viewFirst.BackColor=viewStart==0?Theme.Cyan:Theme.Panel;viewFirst.ForeColor=viewStart==0?Theme.Bg:Theme.Text;viewSecond.BackColor=viewStart==8?Theme.Cyan:Theme.Panel;viewSecond.ForeColor=viewStart==8?Theme.Bg:Theme.Text;
  string profile=profilePath==null?L.T("Профиль не сохранён"):Path.GetFileNameWithoutExtension(profilePath);if(shownProfile!=profile){profileLabel.Text=profile;shownProfile=profile;}
  footer.Text=controllersView?L.T("Настройка контроллера · изменения микшеру не отправляются"):armed.Checked?L.T("LIVE · реальные значения MR18 · Gain относится к физическому входу"):L.T("ТЕСТ · ручные и MIDI-изменения остаются в программе");footer.ForeColor=armed.Checked?Theme.Green:Theme.Amber;
  if(learning)learnStatus.Text=L.T("Ожидание MIDI…\nПодвигайте нужный контрол.");
  if(controllersView)return;
  for(int i=0;i<9;i++){int ch=viewStart+i+1;bool main=i==8;strips[i].Visible=main||mixer.Model==null||ch<=mixer.Model.Channels;string root=main?"/lr":"/ch/"+ch.ToString("00");string name;bool named=mixer.Live&&mixer.Names.TryGetValue(root+"/config/name",out name);name=named?mixer.Names[root+"/config/name"]:main?"MAIN LR":L.T("Канал ")+ch;
   strips[i].UpdateValues(main?0:ch,name,VisibleValue(root+"/mix/fader"),main?null:VisibleValue("/headamp/"+ch.ToString("00")+"/gain"),main?null:VisibleValue(root+"/mix/on"),main?null:VisibleValue("/-stat/solosw/"+ch.ToString("00")),!armed.Checked);
  }
 }
}
public class MixerStrip : Control {
 public bool Main;int channel;string caption="";double? level,gain,mixOn,solo;bool test;bool dragging;
 public event Action<double> LevelChanged;public event Action<string> SwitchClicked;public event Action GainClicked;
 readonly Color[] accents={Theme.Cyan,Color.FromArgb(232,153,63),Color.FromArgb(212,184,71),Color.FromArgb(158,117,209),Theme.Red,Theme.Green,Color.FromArgb(67,147,221),Color.FromArgb(213,104,174)};
 public MixerStrip(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Theme.Panel;ForeColor=Theme.Text;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Slider;TabStop=true;}
 public void UpdateValues(int ch,string name,double? l,double? g,double? on,double? s,bool t){if(channel==ch&&caption==name&&level==l&&gain==g&&mixOn==on&&solo==s&&test==t)return;channel=ch;caption=name;level=l;gain=g;mixOn=on;solo=s;test=t;AccessibleName=(Main?"Main LR":L.T("Канал ")+ch)+" · "+caption;AccessibleDescription=level.HasValue?Osc.FaderToDb(level.Value).ToString("0.0")+" dB":L.T("Ожидание состояния");Invalidate();}
 Rectangle GainRect {get{return new Rectangle(10,91,Width-20,35);}}
 Rectangle MuteRect {get{return new Rectangle(8,141,(Width-22)/2,34);}}
 Rectangle SoloRect {get{return new Rectangle(14+(Width-22)/2,141,(Width-22)/2,34);}}
 int TopTrack {get{return 211;}}int BottomTrack{get{return Math.Max(TopTrack+60,Height-90);}}
 void TextAt(Graphics g,string text,Rectangle r,int size,Color c,bool bold=false,ContentAlignment alignment=ContentAlignment.MiddleLeft){using(var font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)){TextFormatFlags flags=TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix;flags|=alignment==ContentAlignment.MiddleCenter?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left;TextRenderer.DrawText(g,text,font,r,c,flags);}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Theme.Panel);Color accent=Main?Theme.Muted:accents[Math.Max(0,channel-1)%8];using(var b=new SolidBrush(accent))g.FillRectangle(b,0,0,Width,3);using(var p=new Pen(Theme.Border))g.DrawRectangle(p,0,0,Width-1,Height-1);
  TextAt(g,Main?"MAIN LR":channel.ToString("00"),new Rectangle(12,14,Width-24,29),Main?13:17,Theme.Text,true);if(!Main)TextAt(g,caption,new Rectangle(12,43,Width-24,35),10,Theme.Text,true);
  if(!Main){using(var b=new SolidBrush(Theme.Bg))g.FillRectangle(b,GainRect);TextAt(g,"GAIN",new Rectangle(15,91,Width/2-15,35),8,Theme.Muted);TextAt(g,gain.HasValue?(-12+gain.Value*72).ToString("+0.0;-0.0;0.0")+" dB":"—",new Rectangle(Width/2-4,91,Width/2-6,35),9,Theme.Text);
   DrawButton(g,MuteRect,"MUTE",mixOn.HasValue&&mixOn.Value<.5?Theme.Red:Theme.Bg,mixOn.HasValue);DrawButton(g,SoloRect,"SOLO",solo.HasValue&&solo.Value>=.5?Theme.Amber:Theme.Bg,solo.HasValue);
  }
  int top=TopTrack,bottom=BottomTrack,x=Width*2/3;
  foreach(double db in new[]{10d,0,-10,-20,-30,-40,-60,-90}){int y=bottom-(int)(Osc.DbToFader(db)*(bottom-top));using(var p=new Pen(Theme.Border))g.DrawLine(p,Width/2-9,y,Width/2-3,y);TextAt(g,db==-90?"−∞":db.ToString("0"),new Rectangle(8,y-9,Width/2-22,18),8,Theme.Muted,false,ContentAlignment.MiddleCenter);}
  using(var p=new Pen(Theme.Bg,9))g.DrawLine(p,x,top,x,bottom);
  if(level.HasValue){int y=bottom-(int)(level.Value*(bottom-top));using(var p=new Pen(accent,3))g.DrawLine(p,x,y,x,bottom);var thumb=new Rectangle(x-14,y-12,28,24);using(var b=new SolidBrush(Color.FromArgb(208,217,224)))g.FillRectangle(b,thumb);using(var p=new Pen(Color.FromArgb(87,103,118)))g.DrawLine(p,x-13,y,x+13,y);}
  TextAt(g,level.HasValue?Osc.FaderToDb(level.Value).ToString("0.0",CultureInfo.InvariantCulture)+" dB":"— dB",new Rectangle(4,Height-68,Width-8,30),15,level.HasValue?Theme.Cyan:Theme.Muted,true,ContentAlignment.MiddleCenter);
  TextAt(g,test?L.T("ТЕСТ"):level.HasValue?"MR18":L.T("Ожидание"),new Rectangle(4,Height-31,Width-8,22),8,test?Theme.Amber:Theme.Muted,false,ContentAlignment.MiddleCenter);
 }
 void DrawButton(Graphics g,Rectangle r,string text,Color color,bool known){using(var b=new SolidBrush(color))g.FillRectangle(b,r);using(var p=new Pen(Theme.Border))g.DrawRectangle(p,r);TextAt(g,text,r,8,color==Theme.Amber?Theme.Bg:known?Theme.Text:Theme.Muted,true,ContentAlignment.MiddleCenter);}
 void MoveLevel(int y){double v=Osc.Clamp((double)(BottomTrack-y)/(BottomTrack-TopTrack),0,1);if(LevelChanged!=null)LevelChanged(v);}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();if(!Main&&GainRect.Contains(e.Location)){if(GainClicked!=null)GainClicked();return;}if(!Main&&MuteRect.Contains(e.Location)){if(SwitchClicked!=null)SwitchClicked("Mute");return;}if(!Main&&SoloRect.Contains(e.Location)){if(SwitchClicked!=null)SwitchClicked("Solo");return;}if(e.Y>=TopTrack-12&&e.Y<=BottomTrack+12){dragging=true;Capture=true;MoveLevel(e.Y);}}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveLevel(e.Y);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging){MoveLevel(e.Y);dragging=false;Capture=false;}}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=false;}
 protected override bool IsInputKey(Keys keyData){return keyData==Keys.Up||keyData==Keys.Down||base.IsInputKey(keyData);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down){double db=level.HasValue?Osc.FaderToDb(level.Value):-90;if(LevelChanged!=null)LevelChanged(Osc.DbToFader(db+(e.KeyCode==Keys.Up?1:-1)));e.Handled=true;}}
}
}
