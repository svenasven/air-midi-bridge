using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace MR18Midi {
public static class SelfTest {
 static int count;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);count++;Console.WriteLine("PASS "+label);}
 static bool Near(double a,double b){return Math.Abs(a-b)<.00001;}
 static MidiEvent Event(string kind,int value){return new MidiEvent{Kind=kind,Value=value,Maximum=kind=="Pitch"?16383:127,Channel=1,Number=0};}
 public static void Run(){
  foreach(string model in new[]{"MR18","MR18V2","MR12","MR12W","XR12","XR16","XR18","X18"})Check(MixerModel.Find(model)!=null,"Recognize "+model);
  Check(MixerModel.Find("X32")==null&&MixerModel.Find("WING")==null,"Reject unsupported protocol families");
  Check(!MixerModel.Find("XR12").Supports("/ch/13/mix/fader"),"XR12 channel limit");
  Check(!MixerModel.Find("XR16").Supports("/headamp/09/gain"),"XR16 preamp limit");
  Check(MixerModel.Find("XR18").Supports("/headamp/16/gain"),"XR18 preamp 16");
  var packet=Osc.Pack("/headamp/01/gain",.5f);Check(BitConverter.ToString(packet).EndsWith("3F-00-00-00"),"OSC network byte order");
  var msg=Osc.Parse(packet);Check(msg.Address=="/headamp/01/gain"&&Near((float)msg.Args[0],.5),"OSC float roundtrip");
  msg=Osc.Parse(Osc.Pack("/-stat/solosw/01",1));Check(msg.Args[0] is int && (int)msg.Args[0]==1,"OSC integer type");
  msg=Osc.Parse(Osc.Pack("/xinfo","127.0.0.1","Тест","MR18","1.0"));Check((string)msg.Args[1]=="Тест","UTF8 identity");
  Check(Osc.Parse(Osc.Pack("/xinfo")).Args.Length==0,"No-argument query");
  bool bad=false;try{Osc.Parse(new byte[]{47,120,0,0,44,102,0,0});}catch(FormatException){bad=true;}Check(bad,"Reject truncated OSC");
  foreach(double db in new[]{-90d,-75,-60,-40,-30,-20,-10,0,10})Check(Near(Osc.FaderToDb(Osc.DbToFader(db)),db),"Fader dB "+db);
  Check(Near(Osc.DbToFader(0),.75),"Unity fader 0.75");
  Check(new Mapping{Target="Gain",Min=-12,Max=60}.Address=="/headamp/01/gain","Gain physical input address");
  Check(new Mapping{Target="Mute",Mode="Toggle"}.Address=="/ch/01/mix/on","Mute inverse switch address");
  Check(new Mapping{Target="Solo",Mode="Toggle"}.Address=="/-stat/solosw/01","Solo address");
  Check(new Mapping{Target="Gain",Min=-12,Max=61}.Validate()!=null,"Reject gain above +60");
  Check(new Mapping{Channel=17}.Validate()!=null,"Reject physical input 17");
  Check(new Mapping{Step=double.NaN}.Validate()!=null,"Reject NaN");
  Check(new Mapping{Target="Solo",Mode="Gate",Bank=1}.Validate()!=null,"Momentary buttons require global bank");
  var midi=MidiEvent.Decode(0x7F2090);Check(midi.Kind=="Note"&&midi.Number==32&&midi.Pressed,"Note on decode");
  Check(!MidiEvent.Decode(0x7F2080).Pressed,"Note off with nonzero velocity");
  Check(!MidiEvent.Decode(0x002090).Pressed,"Note-on velocity zero");
  Check(MidiEvent.Decode(0x7F7FE7).Value==16383&&MidiEvent.Decode(0x7F7FE7).Channel==8,"14-bit Mackie pitch fader");
  Check(MidiEvent.Decode(0x0000F8)==null,"Ignore MIDI clock");
  var engine=new Engine();var m=new Mapping{Mode="Absolute",Min=-90,Max=0};
  Check(Near(engine.Calculate(m,Event("CC",127),null).Value,.75),"Absolute fader max bounded to 0 dB");
  m=new Mapping{Target="Gain",Mode="Absolute",Min=-12,Max=30};
  Check(Near(engine.Calculate(m,Event("CC",127),null).Value,42d/72),"Gain bounded to configured +30");
  m=new Mapping{Mode="Pickup"};engine.Reset();
  Check(!engine.Calculate(m,Event("CC",0),.5).HasValue,"Pickup blocks jump");
  Check(engine.Calculate(m,Event("CC",100),.5).HasValue,"Pickup catches crossing");
  Check(!engine.Calculate(m,Event("CC",100),.2).HasValue,"External change releases pickup");
  Check(!engine.Calculate(m,Event("CC",100),null).HasValue,"Missing feedback releases pickup");
  m=new Mapping{Target="Mute",Kind="Note",Mode="Toggle"};engine.Reset();
  Check(engine.Calculate(m,Event("Note",127),1)==0,"Mute uses mix/on=0");
  Check(!engine.Calculate(m,Event("Note",100),0).HasValue,"No duplicate toggle while held");
  Check(!engine.Calculate(m,Event("Note",0),0).HasValue,"Release does not toggle");
  Check(engine.Calculate(m,Event("Note",127),0)==1,"Second press unmutes");
  engine.Reset();Check(!engine.Calculate(m,Event("Note",127),null).HasValue,"Toggle requires fresh state");
  m=new Mapping{Target="Solo",Kind="Note",Mode="Gate"};engine.Reset();
  Check(engine.Calculate(m,Event("Note",127),null)==1&&engine.Calculate(m,Event("Note",0),null)==0,"Momentary solo press and release");
  Check(Engine.Relative("Relative2",127)==-1&&Engine.Relative("Relative2",1)==1,"Twos complement encoder");
  Check(Engine.Relative("RelativeOffset",63)==-1&&Engine.Relative("RelativeOffset",65)==1,"Offset encoder");
  Check(Engine.Relative("RelativeSign",65)==-1&&Engine.Relative("RelativeSign",1)==1,"Sign magnitude encoder");
  m=new Mapping{Kind="Note",Mode="Increase",Step=1};engine.Reset();Check(Near(Engine.ToDb(m,engine.Calculate(m,Event("Note",127),Osc.DbToFader(-20)).Value),-19),"Note raises by 1 dB");
  m=new Mapping{Mode="Relative2"};engine.Reset();Check(!engine.Calculate(m,Event("CC",1),null).HasValue,"Relative does not invent missing value");
  var emitted=new List<string>();var batch=new CommandBatch((path,value)=>emitted.Add(path+":"+value));
  for(int i=0;i<128;i++)batch.Add("fader",i,false);batch.Flush();
  Check(emitted.Count==1&&emitted[0]=="fader:127","Rapid fader retains final position, not 128 stale packets");
  emitted.Clear();batch.Add("fader",1,false);batch.Add("mute",0,true);batch.Add("mute",1,true);batch.Add("fader",2,false);batch.Flush();
  Check(string.Join("/",emitted.ToArray())=="fader:1/mute:0/mute:1/fader:2","Buttons preserve every edge and ordering");
  emitted.Clear();batch.Add("a",1,false);batch.Add("b",2,false);batch.Add("a",3,false);batch.Flush();
  Check(emitted.Count==2&&emitted.Contains("a:3")&&emitted.Contains("b:2"),"Independent faders coalesce separately");
  emitted.Clear();batch.Add("fader",127,false);batch.Clear();batch.Flush();Check(emitted.Count==0,"Disarm clears unsent continuous commands");
  var history=new WriteHistory();DateTime stamp=DateTime.UtcNow;
  history.Record("fader",.4,stamp);history.Record("fader",.7,stamp);
  Check(history.IsOldEcho("fader",.4,.7,stamp.AddMilliseconds(100)),"Old own echo recognized");
  Check(!history.IsOldEcho("fader",.7,.7,stamp.AddMilliseconds(100)),"Latest echo accepted");
  Check(!history.IsOldEcho("fader",.2,.7,stamp.AddMilliseconds(100)),"External new value accepted");
  Check(!history.IsOldEcho("fader",.4,.7,stamp.AddMilliseconds(600)),"Echo protection expires");
  history.Clear();Check(!history.IsOldEcho("fader",.7,.4,stamp),"Reconnect clears echo history");
  engine.Reset();m=new Mapping{Mode="Pickup"};double position=.3;
  for(int i=0;i<128;i++){double? result=engine.Calculate(m,Event("CC",i),position);if(result.HasValue)position=result.Value;}
  Check(Near(position,.75),"Pickup handles fast full-travel gesture through final sample");
  var bench=Stopwatch.StartNew();int packets=0;batch=new CommandBatch((path,value)=>packets++);
  for(int frame=0;frame<1000;frame++){for(int i=0;i<128;i++)batch.Add("fader",i,false);batch.Add("mute",0,true);batch.Add("mute",1,true);batch.Flush();}bench.Stop();
  Check(packets==3000,"128000 fader samples plus 2000 button edges -> 3000 ordered packets");
  Console.WriteLine("BENCH: batching 130000 events: "+bench.Elapsed.TotalMilliseconds.ToString("0.00")+" ms (not end-to-end hardware latency)");
  var profile=new Profile();profile.Mappings.Add(m);string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mr18-test-"+Guid.NewGuid()+".xml");try{profile.Save(file);profile.Save(file);var p=Profile.Read(file);Check(p.Mappings.Count==1&&p.Mappings[0].Mode==m.Mode,"Atomic XML profile roundtrip");}finally{if(File.Exists(file))File.Delete(file);}
  using(var server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))using(var mixer=new Mixer()){
   server.Client.ReceiveTimeout=2000;int port=((IPEndPoint)server.Client.LocalEndPoint).Port;mixer.Open("127.0.0.1",port);
   IPEndPoint sender=null;Check(Osc.Parse(server.Receive(ref sender)).Address=="/xinfo","UDP handshake queries before writes");
   Check(!mixer.Live,"Unverified peer cannot be live");
   var identity=Osc.Pack("/xinfo","127.0.0.1","Emulator","X32","1.0");server.Send(identity,identity.Length,sender);Thread.Sleep(20);mixer.Poll();Check(!mixer.Live,"Reject wrong mixer model");
   identity=Osc.Pack("/xinfo","127.0.0.1","Emulator","MR18","1.0");server.Send(identity,identity.Length,sender);Thread.Sleep(20);mixer.Poll();Check(mixer.Live,"MR18 loopback verification");
   var state=Osc.Pack("/ch/01/mix/fader",.75f);server.Send(state,state.Length,sender);Thread.Sleep(20);mixer.Poll();double v;Check(mixer.Fresh("/ch/01/mix/fader",out v)&&Near(v,.75),"UDP feedback cache");
   mixer.Values["/ch/01/mix/fader"].Time=DateTime.UtcNow.AddSeconds(-5);Check(!mixer.Fresh("/ch/01/mix/fader",out v),"Reject stale feedback");
   mixer.Send("/-stat/solosw/01",1);var actual=Osc.Parse(server.Receive(ref sender));Check(actual.Address=="/-stat/solosw/01"&&actual.Args[0] is int,"UDP solo wire payload");
   mixer.SetValue("/ch/01/mix/fader",.4f);server.Receive(ref sender);mixer.SetValue("/ch/01/mix/fader",.75f);server.Receive(ref sender);
   state=Osc.Pack("/ch/01/mix/fader",.4f);server.Send(state,state.Length,sender);Thread.Sleep(20);mixer.Poll();
   Check(mixer.Fresh("/ch/01/mix/fader",out v)&&Near(v,.75),"Delayed UDP echo cannot rewind desired fader and reset pickup");
   state=Osc.Pack("/ch/01/mix/fader",.2f);server.Send(state,state.Length,sender);Thread.Sleep(20);mixer.Poll();
   Check(mixer.Fresh("/ch/01/mix/fader",out v)&&Near(v,.2),"External UDP change remains visible");
   mixer.Dispose();Check(!mixer.Live&&mixer.Values.Count==0,"Disconnect clears state");
  }
  Console.WriteLine("TOTAL: "+count+" checks passed. Real MR18 and physical MIDI hardware not tested.");
 }
}
}
