using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MR18Midi {
public class Mapping {
 [XmlIgnore] public string MidiLabel {get{return Kind+" "+(Kind=="Pitch"?"":Number.ToString())+" · Ch "+MidiChannel;}}
 [XmlIgnore] public string Destination {get{if(IsBusSwitch(Target))return "Bus "+Target.Substring(4)+" "+Target.Substring(0,4);if(IsMonitor(Target))return Target.StartsWith("Aux")?"AUX "+Target.Substring(3)+" Master":"CH "+Channel+" → AUX "+Target.Substring(4);return (Target=="Gain"?L.T("Вход "):"CH ")+Channel.ToString("00")+" · "+Target;}}
 public static string MonitorPath(int channel,int bus,bool master){
  return master?"/bus/"+bus+"/mix/fader":"/ch/"+channel.ToString("00")+"/mix/"+bus.ToString("00")+"/level";
 }
 public static bool IsBusSwitch(string target){return target!=null && System.Text.RegularExpressions.Regex.IsMatch(target,@"^(Mute|Solo)[1-6]$");}
 public static bool IsSwitch(string target){return target=="Mute"||target=="Solo"||IsBusSwitch(target);}
 public static string BusSwitch(int bus,string target){return target.StartsWith("Mute")?"/bus/"+bus+"/mix/on":"/-stat/solosw/"+(21+bus);}
 public static bool IsMonitor(string target){
  return target!=null && System.Text.RegularExpressions.Regex.IsMatch(target,@"^(Send|Aux)[1-6]$");
 }
 public bool Enabled {get;set;}
 public string Name {get;set;}
 public int Bank {get;set;}
 public string Kind {get;set;}
 public int MidiChannel {get;set;}
 public int Number {get;set;}
 public string Target {get;set;}
 public int Channel {get;set;}
 public string Mode {get;set;}
 public double Min {get;set;}
 public double Max {get;set;}
 public double Step {get;set;}
 public Mapping(){Enabled=true;Name=L.T("Назначение");Bank=1;Kind="CC";MidiChannel=1;Target="Fader";Channel=1;Mode="Pickup";Min=-90;Max=0;Step=1;}
 public string Validate() {
  if(Bank<0 || Bank>8 || MidiChannel<1 || MidiChannel>16 || Number<0 || Number>127 || Channel<1 || Channel>16) return L.T("Банк 0–8, MIDI-канал 1–16, номер 0–127, канал MR18 1–16.");
  if(!new[]{"CC","Note","Pitch"}.Contains(Kind)) return L.T("Неизвестный тип MIDI.");
  if(!IsBusSwitch(Target)&&!IsMonitor(Target)&&!new[]{"Fader","Gain","Mute","Solo","Main","Bank+","Bank-"}.Contains(Target)) return L.T("Неизвестная цель.");
  if(!new[]{"Pickup","Absolute","Relative2","RelativeOffset","RelativeSign","Toggle","Gate","Increase","Decrease"}.Contains(Mode)) return L.T("Неизвестный режим.");
  if(double.IsNaN(Min)||double.IsNaN(Max)||double.IsNaN(Step)||double.IsInfinity(Min)||double.IsInfinity(Max)||double.IsInfinity(Step)||Step<=0||Step>12) return L.T("Шаг должен быть больше 0 и не более 12 dB.");
  if(IsSwitch(Target)) {if(Mode=="Gate"&&Bank!=0)return L.T("Для Gate используйте банк 0, чтобы отпускание кнопки работало при смене банка.");return (Mode=="Toggle" || Mode=="Gate") ? null : L.T("Для Mute/Solo выберите Toggle или Gate.");}
  if(Target.StartsWith("Bank")) return null;
  double lo=Target=="Gain"?-12:-90, hi=Target=="Gain"?60:10;
  if(Min<lo||Max>hi||Min>=Max) return L.T("Диапазон ") + Target + ": " + lo + "…" + hi + L.T(" dB; минимум должен быть меньше максимума.");
  if(Mode=="Toggle"||Mode=="Gate") return L.T("Для уровня выберите Pickup, Absolute, Relative или Increase/Decrease.");
  if(Kind=="Note" && Mode!="Increase" && Mode!="Decrease") return L.T("Для клавиш уровня выберите Increase или Decrease.");
  if(Kind!="CC" && Mode.StartsWith("Relative")) return L.T("Relative поддерживает CC.");
  return null;
 }
 [XmlIgnore] public string Address {get {
  if(IsBusSwitch(Target))return BusSwitch(Target[4]-'0',Target);if(IsMonitor(Target))return MonitorPath(Channel,Target[Target.Length-1]-'0',Target.StartsWith("Aux")); if(Target=="Gain") return "/headamp/"+Channel.ToString("00")+"/gain";
  if(Target=="Solo") return "/-stat/solosw/"+Channel.ToString("00");
  if(Target=="Main") return "/lr/mix/fader";
  return "/ch/"+Channel.ToString("00")+"/mix/"+(Target=="Mute"?"on":"fader");
 }}
}
public class Profile {
 public string Host {get;set;}
 public string Device {get;set;}
 public List<Mapping> Mappings {get;set;}
 public Profile(){Host="192.168.1.1";Device="";Mappings=new List<Mapping>();}
 public static Profile Read(string path) { using(var s=File.OpenRead(path)) return (Profile)new XmlSerializer(typeof(Profile)).Deserialize(s); }
 public void Save(string path) {
  string tmp=path+".tmp";
  using(var s=File.Create(tmp)) new XmlSerializer(typeof(Profile)).Serialize(s,this);
  if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
 }
}
public class MidiEvent {
 public string Kind; public int Channel,Number,Value,Maximum;
 public bool Pressed {get {return Value>0;}}
 public static MidiEvent Decode(uint packed) {
  int s=(int)(packed&255), a=(int)((packed>>8)&127), b=(int)((packed>>16)&127), type=s&240;
  var e=new MidiEvent{Channel=(s&15)+1,Number=a,Value=b,Maximum=127};
  if(type==176) e.Kind="CC";
  else if(type==144||type==128) {e.Kind="Note";if(type==128)e.Value=0;}
  else if(type==224) {e.Kind="Pitch";e.Number=0;e.Value=a+(b<<7);e.Maximum=16383;}
  else return null;
  return e;
 }
 public bool Matches(Mapping m) {return Kind==m.Kind && Channel==m.MidiChannel && (Kind=="Pitch"||Number==m.Number);}
 public override string ToString() {return Kind+" ch="+Channel+" #"+Number+" = "+Value;}
}
public class MidiInput : IDisposable {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Caps {
  public ushort Mid,Pid; public uint Version;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name;
  public uint Support;
 }
 delegate void Callback(IntPtr handle,uint msg,UIntPtr instance,UIntPtr p1,UIntPtr p2);
 [DllImport("winmm.dll")] static extern uint midiInGetNumDevs();
 [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern uint midiInGetDevCapsW(UIntPtr id,out Caps caps,uint size);
 [DllImport("winmm.dll")] static extern uint midiInOpen(out IntPtr h,uint id,Callback cb,UIntPtr instance,uint flags);
 [DllImport("winmm.dll")] static extern uint midiInStart(IntPtr h);
 [DllImport("winmm.dll")] static extern uint midiInStop(IntPtr h);
 [DllImport("winmm.dll")] static extern uint midiInReset(IntPtr h);
 [DllImport("winmm.dll")] static extern uint midiInClose(IntPtr h);
 IntPtr handle; Callback callback;
 public ConcurrentQueue<MidiEvent> Queue=new ConcurrentQueue<MidiEvent>();
 public static string[] Devices() {
  var names=new List<string>();
  for(uint i=0;i<midiInGetNumDevs();i++){Caps c;if(midiInGetDevCapsW((UIntPtr)i,out c,(uint)Marshal.SizeOf(typeof(Caps)))==0)names.Add(c.Name);else names.Add("MIDI "+i);}
  return names.ToArray();
 }
 public void Open(int id) {
  Dispose();
  callback=(h,msg,inst,p1,p2)=> {if(msg==0x3C3 && Queue.Count<2048){var e=MidiEvent.Decode((uint)p1.ToUInt64());if(e!=null)Queue.Enqueue(e);}};
  uint err=midiInOpen(out handle,(uint)id,callback,UIntPtr.Zero,0x30000);
  if(err!=0){handle=IntPtr.Zero;throw new Exception(L.T("Не удалось открыть MIDI (код ")+err+L.T("). Закройте другие программы, использующие этот MIDI-вход."));}
  err=midiInStart(handle); if(err!=0){Dispose();throw new Exception(L.T("Не удалось запустить MIDI: ")+err);}
 }
 public void Dispose(){if(handle!=IntPtr.Zero){midiInStop(handle);midiInReset(handle);midiInClose(handle);handle=IntPtr.Zero;}MidiEvent e;while(Queue.TryDequeue(out e)){} }
}
public class OscMessage {public string Address;public object[] Args;}
public static class Osc {
 static void Str(Stream s,string value){var b=Encoding.UTF8.GetBytes(value);s.Write(b,0,b.Length);s.WriteByte(0);while(s.Length%4!=0)s.WriteByte(0);}
 public static byte[] Pack(string address,params object[] args){
  using(var s=new MemoryStream()){
   Str(s,address);
   if(args.Length>0){Str(s,","+string.Concat(args.Select(x=>x is string?"s":x is int?"i":"f")));
    foreach(var a in args){if(a is string){Str(s,(string)a);continue;}byte[] b=a is int?BitConverter.GetBytes((int)a):BitConverter.GetBytes(Convert.ToSingle(a));if(BitConverter.IsLittleEndian)Array.Reverse(b);s.Write(b,0,4);}}
   return s.ToArray();
  }
 }
 static string ReadString(byte[] b,ref int i){int start=i;while(i<b.Length && b[i]!=0)i++;if(i==b.Length)throw new FormatException();string s=Encoding.UTF8.GetString(b,start,i-start);i=(i+4)&~3;if(i>b.Length)throw new FormatException();return s;}
 public static OscMessage Parse(byte[] b){
  if(b==null||b.Length<4||b.Length%4!=0)throw new FormatException();
  int i=0;string address=ReadString(b,ref i);if(!address.StartsWith("/"))throw new FormatException();
  var args=new List<object>();if(i<b.Length){string tags=ReadString(b,ref i);if(!tags.StartsWith(","))throw new FormatException();foreach(char t in tags.Substring(1)){
   if(t=='s'){args.Add(ReadString(b,ref i));continue;}if(t!='i'&&t!='f')throw new FormatException();if(i+4>b.Length)throw new FormatException();byte[] n=new byte[4];Array.Copy(b,i,n,0,4);i+=4;if(BitConverter.IsLittleEndian)Array.Reverse(n);if(t=='i')args.Add(BitConverter.ToInt32(n,0));else args.Add(BitConverter.ToSingle(n,0));
  }}
  if(i!=b.Length)throw new FormatException();return new OscMessage{Address=address,Args=args.ToArray()};
 }
 public static double FaderToDb(double x){return x>=.5?40*x-30:x>=.25?80*x-50:x>=.0625?160*x-70:480*x-90;}
 public static double DbToFader(double db){db=Clamp(db,-90,10);return db>=-10?(db+30)/40:db>=-30?(db+50)/80:db>=-60?(db+70)/160:(db+90)/480;}
 public static double Clamp(double x,double lo,double hi){return Math.Max(lo,Math.Min(hi,x));}
}
public class State {public double Value; public DateTime Time;}
// Coalesce only continuous output. Buttons form an ordering boundary and are never dropped.
public class CommandBatch {
 readonly Dictionary<string,object> pending=new Dictionary<string,object>();
 readonly List<string> order=new List<string>();
 readonly Action<string,object> send;
 public CommandBatch(Action<string,object> send){this.send=send;}
 public void Add(string path,object value,bool button){
  if(button){Flush();send(path,value);return;}
  if(!pending.ContainsKey(path))order.Add(path);pending[path]=value;
 }
 public void Flush(){var paths=order.ToArray();var values=paths.Select(p=>pending[p]).ToArray();Clear();for(int i=0;i<paths.Length;i++)send(paths[i],values[i]);}
 public void Clear(){pending.Clear();order.Clear();}
}
// /xremote can echo an earlier position after a newer MIDI write was already sent.
public class WriteHistory {
 readonly Dictionary<string,Queue<State>> sent=new Dictionary<string,Queue<State>>();
 public void Record(string path,double value,DateTime now){Queue<State> q;if(!sent.TryGetValue(path,out q)){q=new Queue<State>();sent[path]=q;}q.Enqueue(new State{Value=value,Time=now});while(q.Count>64)q.Dequeue();}
 public bool IsOldEcho(string path,double incoming,double desired,DateTime now){
  if(Math.Abs(incoming-desired)<.001)return false;
  Queue<State> q;if(!sent.TryGetValue(path,out q))return false;
  while(q.Count>0&&(now-q.Peek().Time).TotalMilliseconds>500)q.Dequeue();
  return q.Any(s=>Math.Abs(s.Value-incoming)<.001);
 }
 public void Clear(){sent.Clear();}
}
public class Mixer : IDisposable {
 UdpClient udp;
 readonly WriteHistory writes=new WriteHistory();
 public MixerModel Model {get;private set;}
 public bool SupportsAddress(string path){return Model==null||Model.Supports(path);}
 public bool Verified {get;private set;}
 public string Identity {get;private set;}
 public DateTime LastIdentity {get;private set;}
 public Mixer(){Identity=L.T("Нет соединения");LastIdentity=DateTime.MinValue;}
 public Dictionary<string,string> Names=new Dictionary<string,string>();
 public Dictionary<string,State> Values=new Dictionary<string,State>();
 public bool Live {get {return udp!=null&&Verified&&(DateTime.UtcNow-LastIdentity).TotalSeconds<9;}}
 public bool Opened {get {return udp!=null;}}
 public void Open(string ip,int port=10024){
  IPAddress addr;if(!IPAddress.TryParse(ip,out addr)||addr.AddressFamily!=AddressFamily.InterNetwork)throw new Exception(L.T("Укажите IPv4-адрес MR18."));
  Dispose();udp=new UdpClient(AddressFamily.InterNetwork);udp.Connect(addr,port);udp.Client.Blocking=false;Send("/xinfo");
 }
 public void Send(string address,params object[] args){if(udp==null)throw new InvalidOperationException(L.T("Нет подключения"));var b=Osc.Pack(address,args);udp.Send(b,b.Length);}
 public void SetValue(string address,object value){if(Model==null||!Model.Supports(address))throw new InvalidOperationException("Parameter is not available on this mixer model.");Send(address,value);double v=Convert.ToDouble(value);writes.Record(address,v,DateTime.UtcNow);Values[address]=new State{Value=v,Time=DateTime.UtcNow};}
 public void Poll(){
  if(udp==null)return;
  for(int n=0;n<256 && udp.Available>0;n++){
   IPEndPoint ep=null;byte[] b=udp.Receive(ref ep);OscMessage msg;try{msg=Osc.Parse(b);}catch(FormatException){continue;}
   if(msg.Address=="/xinfo" && msg.Args.Length>=3){
    string model=msg.Args[2] as string;
    Model=MixerModel.Find(model);Verified=Model!=null;
    Identity=string.Join(" · ",msg.Args.Select(x=>Convert.ToString(x)).ToArray());LastIdentity=DateTime.UtcNow;
   }
   if(msg.Args.Length==1&&msg.Args[0] is string&&msg.Address.EndsWith("/config/name"))Names[msg.Address]=(string)msg.Args[0];
   if(msg.Args.Length==1 && (msg.Args[0] is float || msg.Args[0] is int)){
    double v=Convert.ToDouble(msg.Args[0]);if(!double.IsNaN(v)&&!double.IsInfinity(v)){State desired;if(!Values.TryGetValue(msg.Address,out desired)||!writes.IsOldEcho(msg.Address,v,desired.Value,DateTime.UtcNow))Values[msg.Address]=new State{Value=v,Time=DateTime.UtcNow};}
   }
  }
 }
 public bool Fresh(string path,out double v){State s;if(Values.TryGetValue(path,out s)&&(DateTime.UtcNow-s.Time).TotalSeconds<4){v=s.Value;return true;}v=0;return false;}
 public void Dispose(){if(udp!=null)udp.Close();udp=null;Model=null;Verified=false;LastIdentity=DateTime.MinValue;Values.Clear();Names.Clear();writes.Clear();Identity=L.T("Нет соединения");}
}
public class Runtime {public bool Captured,Down;public double? Previous;public double? LastSent;}
public class Engine {
 public Dictionary<Mapping,Runtime> Runtimes=new Dictionary<Mapping,Runtime>();
 public void Reset(){Runtimes.Clear();}
 public double? Calculate(Mapping m,MidiEvent e,double? current){
  Runtime r;if(!Runtimes.TryGetValue(m,out r)){r=new Runtime();Runtimes[m]=r;}
  bool rising=e.Pressed&&!r.Down;r.Down=e.Pressed;
  if(Mapping.IsSwitch(m.Target)){
   if(m.Mode=="Gate")return m.Target.StartsWith("Mute")?(e.Pressed?0:1):(e.Pressed?1:0);
   if(!rising||!current.HasValue)return null;return current.Value>=.5?0:1;
  }
  if(e.Kind=="Note"&&!rising)return null;
  double db;
  if(m.Mode=="Increase"||m.Mode=="Decrease"){
   if(!rising||!current.HasValue)return null;db=ToDb(m,current.Value)+(m.Mode=="Increase"?m.Step:-m.Step);
  }else if(m.Mode.StartsWith("Relative")){
   if(!current.HasValue)return null;int delta=Relative(m.Mode,e.Value);if(delta==0)return null;db=ToDb(m,current.Value)+delta*m.Step;
  }else {
   double x=(double)e.Value/e.Maximum;
   if(m.Target=="Gain")db=m.Min+x*(m.Max-m.Min);
   else db=Osc.FaderToDb(Osc.DbToFader(m.Min)+x*(Osc.DbToFader(m.Max)-Osc.DbToFader(m.Min)));
   double desired=FromDb(m,db);
   if(m.Mode=="Pickup"){
    if(!current.HasValue){r.Captured=false;r.Previous=desired;return null;}
    // A change by another client releases pickup. Own echoed values keep it captured.
    if(r.Captured&&r.LastSent.HasValue&&Math.Abs(current.Value-r.LastSent.Value)>.012)r.Captured=false;
    bool crossed=r.Previous.HasValue && (r.Previous.Value-current.Value)*(desired-current.Value)<=0;
    r.Previous=desired;
    if(!r.Captured && Math.Abs(desired-current.Value)>.012&&!crossed)return null;
    r.Captured=true;
   }
  }
  double value=FromDb(m,Osc.Clamp(db,m.Min,m.Max));r.LastSent=value;return value;
 }
 public static int Relative(string mode,int v){if(mode=="RelativeOffset")return v-64;if(mode=="RelativeSign")return v<64?v:-(v-64);return v<64?v:v-128;}
 public static double ToDb(Mapping m,double x){return m.Target=="Gain"?-12+x*72:Osc.FaderToDb(x);}
 public static double FromDb(Mapping m,double db){return m.Target=="Gain"?(db+12)/72:Osc.DbToFader(db);}
}
public partial class MainForm : Form {
 TextBox host=new TextBox{Width=140};ComboBox devices=new ComboBox{Width=230,DropDownStyle=ComboBoxStyle.DropDownList};
 Button connect=new Button{Text=L.T("Подключить MR18"),AutoSize=true},midiButton=new Button{Text=L.T("Открыть MIDI"),AutoSize=true};
 CheckBox armed=new CheckBox{Text=L.T("Управлять микшером"),AutoSize=true};
 Label status=new Label{AutoSize=true,Text=L.T("ТЕСТ: команды только в журнал")},midiStatus=new Label{AutoSize=true,Text=L.T("MIDI закрыт")};
 NumericUpDown bank=new NumericUpDown{Minimum=1,Maximum=8,Value=1,Width=45};
 DataGridView grid=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
 TextBox log=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=new Font("Consolas",9)};
 BindingList<Mapping> mappings=new BindingList<Mapping>();MidiInput midi=new MidiInput();Mixer mixer=new Mixer();Engine engine=new Engine();
 Dictionary<string,State> simulation=new Dictionary<string,State>();
 System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=10};
 CommandBatch commands;
 readonly Queue<string> logBuffer=new Queue<string>();readonly Dictionary<string,string> continuousLog=new Dictionary<string,string>();
 DateTime nextLog=DateTime.MinValue;string latestMidiText;
 DateTime nextInfo=DateTime.MinValue,nextSync=DateTime.MinValue; Queue<string> queries=new Queue<string>(); DateTime nextQuery=DateTime.MinValue;
 bool midiOpen,learning;Mapping learnTarget;int lines;string profilePath;bool closing;
 public MainForm(){
  commands=new CommandBatch((path,value)=>{if(armed.Checked&&mixer.Live)mixer.SetValue(path,value);});
  Text="AIR MIDI Bridge · 1.0.0";Size=new Size(1460,940);MinimumSize=new Size(1180,780);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);
  BuildInterface();
  connect.Click+=(s,e)=>Guard(ToggleMixer);midiButton.Click+=(s,e)=>Guard(ToggleMidi);
  armed.CheckedChanged+=(s,e)=>{
   commands.Clear();if(armed.Checked){if(controllersView){armed.Checked=false;Write(L.T("Включайте управление на вкладке Микшер. Настройка контроллеров выполняется в тестовом режиме."));return;}if(!Commit()){armed.Checked=false;return;}if(!mixer.Live){armed.Checked=false;Write(L.T("Сначала подключите MR18 и дождитесь подтверждения модели."));return;}simulation.Clear();engine.Reset();Write(L.T("Управление MR18 включено."));}else{engine.Reset();Write(L.T("Тестовый режим: запись в MR18 отключена."));}RefreshVisuals(true);
  };
  bank.ValueChanged+=(s,e)=>{commands.Flush();engine.Reset();if(bank.Value<=2)viewStart=bank.Value==1?0:8;Write(L.T("Банк ")+bank.Value);RefreshVisuals(true);};
  timer.Tick+=(s,e)=>Tick();FormClosing+=(s,e)=>{closing=true;timer.Stop();midi.Dispose();mixer.Dispose();};
  RefreshDevices();host.Text="192.168.1.1";AddMapping();ShowPage(false);ApplyLanguage();timer.Start();
  Write(L.T("Начните с вкладки Контроллеры: выберите MIDI, загрузите профиль или создайте назначение. По умолчанию включён ТЕСТ."));
 }
 void AddButton(Control p,string name,Action action){var b=new Button{Text=name,AutoSize=true};b.Click+=(s,e)=>Guard(action);p.Controls.Add(b);}
 void ColText(string prop,string title,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=prop,HeaderText=title,FillWeight=width});}
 void ColCheck(string prop,string title,int width){grid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName=prop,HeaderText=title,FillWeight=width});}
 void ColCombo(string prop,string title,string[] values,int width){var c=new DataGridViewComboBoxColumn{DataPropertyName=prop,HeaderText=title,FillWeight=width};c.Items.AddRange(values);grid.Columns.Add(c);}
 void Guard(Action a){try{a();}catch(Exception e){Disarm();Write(L.T("Ошибка: ")+e.Message);}}
 void Write(string s){if(closing)return;if(logBuffer.Count>=200)logBuffer.Dequeue();logBuffer.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff")+"  "+s);}
 void FlushLog(){
  if(DateTime.UtcNow<nextLog)return;nextLog=DateTime.UtcNow.AddMilliseconds(100);
  foreach(var s in continuousLog.Values)Write(s);continuousLog.Clear();
  if(logBuffer.Count==0)return;lines+=logBuffer.Count;if(lines>500){log.Clear();lines=logBuffer.Count;}
  string batch=string.Join(Environment.NewLine,logBuffer.ToArray());logBuffer.Clear();log.AppendText(batch+Environment.NewLine);
 }
 void Disarm(){commands.Clear();armed.Checked=false;}
 bool Commit(){
  if(!grid.EndEdit())return false;BindingContext[mappings].EndCurrentEdit();
  foreach(var m in mappings){if(m.Enabled&&mixer.Model!=null&&!m.Target.StartsWith("Bank")&&!mixer.Model.Supports(m.Address)){Write("Unsupported mapping on "+mixer.Model.Name+": "+m.Name);return false;}string err=m.Validate();if(err!=null){Write(m.Name+": "+err);return false;}}
  var active=mappings.Where(m=>m.Enabled).ToList();
  for(int a=0;a<active.Count;a++)for(int b=a+1;b<active.Count;b++){
   var x=active[a];var y=active[b];if(x.MidiChannel==y.MidiChannel&&x.Kind==y.Kind&&(x.Kind=="Pitch"||x.Number==y.Number)&&(x.Bank==0||y.Bank==0||x.Bank==y.Bank)){Write(L.T("Один MIDI-контрол назначен дважды в активных банках: ")+x.Name+" / "+y.Name);return false;}
  }
  return true;
 }
 void RefreshDevices(){if(midiOpen){Write(L.T("Сначала закройте MIDI-вход."));return;}string selected=devices.Text;devices.Items.Clear();devices.Items.AddRange(MidiInput.Devices());if(devices.Items.Count>0){int i=devices.Items.IndexOf(selected);devices.SelectedIndex=i<0?0:i;}}
 void ToggleMidi(){Disarm();learning=false;if(midiOpen){midi.Dispose();midiOpen=false;midiButton.Text=L.T("Открыть MIDI");devices.Enabled=true;midiStatus.Text=L.T("MIDI закрыт");return;}if(devices.SelectedIndex<0)throw new Exception(L.T("MIDI-входы не найдены. Подключите контроллер и нажмите Обновить MIDI."));midi.Open(devices.SelectedIndex);midiOpen=true;devices.Enabled=false;midiButton.Text=L.T("Закрыть MIDI");midiStatus.Text=L.T("Ожидание MIDI");}
 void ToggleMixer(){Disarm();queries.Clear();engine.Reset();simulation.Clear();if(mixer.Opened){mixer.Dispose();connect.Text=L.T("Подключить MR18");host.Enabled=true;return;}mixer.Open(host.Text.Trim());connect.Text=L.T("Отключить MR18");host.Enabled=false;nextInfo=DateTime.UtcNow.AddSeconds(3);nextSync=DateTime.MinValue;Write(L.T("Запрошена модель MR18; пока отправляются только запросы состояния."));}
 void AddMapping(){Disarm();var m=new Mapping{Name=L.T("Канал ")+(mappings.Count+1),Channel=Math.Min(16,mappings.Count+1),Number=Math.Min(127,mappings.Count)};mappings.Add(m);if(grid.Rows.Count==mappings.Count){grid.ClearSelection();grid.Rows[mappings.Count-1].Selected=true;grid.CurrentCell=grid.Rows[mappings.Count-1].Cells[1];}}
 Mapping Selected(){return grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as Mapping;}
 void Learn(){Disarm();if(!midiOpen)throw new Exception(L.T("Откройте MIDI-вход."));learnTarget=Selected();if(learnTarget==null)return;grid.EndEdit();learning=true;Write(L.T("LEARN: двигайте нужную ручку/ползунок или нажмите кнопку. Это событие не отправится микшеру."));}
 void Delete(){Disarm();learning=false;var m=Selected();if(m!=null)mappings.Remove(m);engine.Reset();}
 void Example(){
  Disarm();if(mappings.Count>1 && MessageBox.Show(L.T("Заменить текущие назначения примером?"),L.T("Пример"),MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
  mappings.Clear();for(int i=0;i<8;i++){
   mappings.Add(new Mapping{Name="Fader "+(i+1),Kind="CC",Number=i,Channel=i+1});
   mappings.Add(new Mapping{Name=L.T("Gain вход ")+(i+1),Kind="CC",Number=16+i,Channel=i+1,Target="Gain",Min=-12,Max=30});
   mappings.Add(new Mapping{Name="Mute "+(i+1),Kind="Note",Number=32+i,Channel=i+1,Target="Mute",Mode="Toggle"});
   mappings.Add(new Mapping{Name="Solo "+(i+1),Kind="Note",Number=48+i,Channel=i+1,Target="Solo",Mode="Toggle"});
  }engine.Reset();Write(L.T("Пример создан. Это НЕ заводской профиль M-VAVE: назначьте реальные контролы через Learn."));
 }
 void LoadProfile(){
  Disarm();using(var d=new OpenFileDialog{Filter=L.T("Профиль MR18 (*.xml)|*.xml"),InitialDirectory=AppDomain.CurrentDomain.BaseDirectory}){if(d.ShowDialog()!=DialogResult.OK)return;var p=Profile.Read(d.FileName);if(p.Mappings==null||p.Mappings.Count>1000)throw new Exception(L.T("Некорректный профиль."));foreach(var m in p.Mappings)if(m.Validate()!=null)throw new Exception(m.Name+": "+m.Validate());
   if(midiOpen)ToggleMidi();if(mixer.Opened)ToggleMixer();learning=false;simulation.Clear();mappings.Clear();foreach(var m in p.Mappings)mappings.Add(m);host.Text=p.Host;int index=devices.Items.IndexOf(p.Device);if(index>=0)devices.SelectedIndex=index;profilePath=d.FileName;engine.Reset();Write(L.T("Профиль загружен; MIDI и MR18 подключаются вручную."));}
 }
 void SaveProfile(){if(!Commit())return;using(var d=new SaveFileDialog{Filter=L.T("Профиль MR18 (*.xml)|*.xml"),FileName=profilePath??"my-controller.xml",InitialDirectory=AppDomain.CurrentDomain.BaseDirectory}){if(d.ShowDialog()!=DialogResult.OK)return;new Profile{Host=host.Text.Trim(),Device=devices.Text,Mappings=mappings.ToList()}.Save(d.FileName);profilePath=d.FileName;Write(L.T("Сохранено: ")+profilePath);}}
 void Tick(){
  try{
   mixer.Poll();DateTime now=DateTime.UtcNow;
   if(mixer.Opened){
    if(now>=nextInfo){mixer.Send("/xinfo");if(mixer.Live)mixer.Send("/xremote");nextInfo=now.AddSeconds(3);}
    if(mixer.Live&&now>=nextSync&&queries.Count==0){foreach(string path in DisplayPaths().Concat(mappings.Where(m=>m.Enabled&&!m.Target.StartsWith("Bank")&&m.Validate()==null).Select(m=>m.Address)).Where(mixer.SupportsAddress).Distinct())queries.Enqueue(path);nextSync=now.AddSeconds(2);}
    if(queries.Count>0&&mixer.Live&&now>=nextQuery){mixer.Send(queries.Dequeue());nextQuery=now.AddMilliseconds(20);}
   }
   if(armed.Checked&&!mixer.Live){Disarm();engine.Reset();Write(L.T("Нет подтверждения MR18. Управление отключено; повторное включение вручную."));}
   status.Text=armed.Checked?"LIVE · "+mixer.Identity:mixer.Live?L.T("ТЕСТ · MR18 подключён"):L.T("ТЕСТ · ")+mixer.Identity;
   status.ForeColor=armed.Checked?Color.Firebrick:Color.DarkSlateGray;
   // Capture the batch boundary so a continuously active producer cannot starve the UI.
   int available=Math.Min(2048,midi.Queue.Count);MidiEvent e;
   for(int i=0;i<available&&midi.Queue.TryDequeue(out e);i++)HandleMidi(e);
   commands.Flush();
   if(latestMidiText!=null){midiStatus.Text=latestMidiText;latestMidiText=null;}
   FlushLog();RefreshVisuals(false);
  }catch(Exception ex){Disarm();mixer.Dispose();queries.Clear();connect.Text=L.T("Подключить MR18");host.Enabled=true;Write(L.T("Связь: ")+ex.Message);}
 }
 void HandleMidi(MidiEvent e){
  latestMidiText=e.ToString();
  if(learning){if(e.Kind=="Note"&&!e.Pressed)return;learning=false;if(!mappings.Contains(learnTarget))return;learnTarget.Kind=e.Kind;learnTarget.MidiChannel=e.Channel;learnTarget.Number=e.Number;
   if(e.Kind=="Note" && !Mapping.IsSwitch(learnTarget.Target)&&!learnTarget.Target.StartsWith("Bank"))learnTarget.Mode="Increase";
   mappings.ResetBindings();engine.Reset();LoadInspector();Write("LEARN: "+learnTarget.Name+" ← "+e);return;
  }
  if(grid.IsCurrentCellInEditMode)return;
  var matches=mappings.Where(m=>m.Enabled&&(m.Bank==0||m.Bank==(int)bank.Value)&&e.Matches(m)&&m.Validate()==null).ToList();
  if(matches.Count>1){Write(L.T("Конфликт назначений для ")+e+L.T(". Событие пропущено."));return;}
  foreach(var m in matches){
   if(m.Target.StartsWith("Bank")){Runtime rt;if(!engine.Runtimes.TryGetValue(m,out rt)){rt=new Runtime();engine.Runtimes[m]=rt;}bool press=e.Pressed&&!rt.Down;rt.Down=e.Pressed;if(press)bank.Value=(decimal)Osc.Clamp((int)bank.Value+(m.Target=="Bank+"?1:-1),1,8);continue;}
   if(mixer.Model!=null&&!mixer.Model.Supports(m.Address))continue;
   double current;double? state=null;
   if(armed.Checked){if(mixer.Fresh(m.Address,out current))state=current;}
   else {State simulated;if(simulation.TryGetValue(m.Address,out simulated))state=simulated.Value;else if(mixer.Fresh(m.Address,out current))state=current;else state=m.Target.StartsWith("Mute")?1:Engine.FromDb(m,m.Target=="Gain"?0:-30);}
   double? value=engine.Calculate(m,e,state);
   if(!value.HasValue){if(armed.Checked&&!state.HasValue)mixer.Send(m.Address);return;}
   bool discrete=Mapping.IsSwitch(m.Target);object payload=discrete?(object)(int)value.Value:(object)(float)value.Value;
   if(armed.Checked){if(!mixer.Live)return;commands.Add(m.Address,payload,discrete);mixer.Values[m.Address]=new State{Value=value.Value,Time=DateTime.UtcNow};}
   else simulation[m.Address]=new State{Value=value.Value,Time=DateTime.UtcNow};
   string message=(armed.Checked?"LIVE ":"TEST ")+m.Name+" → "+m.Address+" "+(discrete?Convert.ToString(payload):Engine.ToDb(m,value.Value).ToString("0.0",CultureInfo.InvariantCulture)+" dB");
   if(discrete)Write(message);else continuousLog[m.Address]=message;
  }
 }
}
public static class Program {
 [STAThread] public static int Main(string[] args){
  if(args.Contains("--self-test")){try{SelfTest.Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
  if(args.Contains("--ui-test")){try{Application.EnableVisualStyles();UiTests.Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
  try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);L.Load();Application.Run(new MainForm());return 0;}
  catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.txt"),e.ToString());return 1;}
 }
}
}
