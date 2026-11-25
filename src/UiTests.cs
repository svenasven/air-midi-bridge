using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;

namespace MR18Midi {
public static class UiTests {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL UI: "+name);checks++;Console.WriteLine("PASS UI "+name);}
 static object Get(object obj,string name){return obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);}
 public static void Run(){L.English=false;using(var form=new MainForm()){
  form.Show();Application.DoEvents();
  Check(form.Text.EndsWith("0.5-beta"),"Version and construction");
  Check(form.FormBorderStyle==FormBorderStyle.None,"Native light caption removed");
  Check(MainForm.ResizeHit(new Point(1,1),new Size(1200,800),6)==13&&MainForm.ResizeHit(new Point(1198,798),new Size(1200,800),6)==17,"Resize corners");
  Check(MainForm.ResizeHit(new Point(1,400),new Size(1200,800),6)==10&&MainForm.ResizeHit(new Point(600,798),new Size(1200,800),6)==15,"Resize edges");
  Check(MainForm.ResizeHit(new Point(600,400),new Size(1200,800),6)==1,"Resize leaves controls interactive");
  Call(form,"ToggleMaximized");Application.DoEvents();Check(form.WindowState==FormWindowState.Maximized,"Custom maximize");
  Call(form,"ToggleMaximized");Application.DoEvents();Check(form.WindowState==FormWindowState.Normal,"Custom restore");
  var arm=(CheckBox)Get(form,"armed");Check(!arm.Checked,"Starts in test mode");
  Call(form,"DirectLevel",0,.75);var sim=(Dictionary<string,State>)Get(form,"simulation");
  Check(sim.ContainsKey("/ch/01/mix/fader")&&Math.Abs(sim["/ch/01/mix/fader"].Value-.75)<.00001,"Screen fader uses OSC scale in simulation");
  Call(form,"DirectSwitch",0,"Mute");Check(sim["/ch/01/mix/on"].Value==0,"Screen mute inverse logic");
  Call(form,"DirectSwitch",0,"Mute");Check(sim["/ch/01/mix/on"].Value==1,"Screen unmute");
  Call(form,"DirectSwitch",0,"Solo");Check(sim["/-stat/solosw/01"].Value==1,"Screen solo");
  Call(form,"DirectLevel",8,.5);Check(sim["/lr/mix/fader"].Value==.5,"Master fader address");
  var page=(Panel)Get(form,"controllerPage");Call(form,"ShowPage",true);Application.DoEvents();Check(page.Visible&&!arm.Checked,"Controller page disarms");
  arm.Checked=true;Check(!arm.Checked,"Cannot enable live on configuration page");
  int size=sim.Count;Call(form,"DirectLevel",1,.8);Check(sim.Count==size,"Configuration page ignores manual mixer input");
  var mappings=(BindingList<Mapping>)Get(form,"mappings");var name=(TextBox)Get(form,"editName");name.Text="Проверка профиля";
  Check((bool)Call(form,"ApplyInspector",false),"Inspector applies valid draft");Check(mappings[0].Name=="Проверка профиля","Draft persisted in mapping");
  var target=(ComboBox)Get(form,"editTarget");target.SelectedValue="Mute";
  Check(Convert.ToString(((ComboBox)Get(form,"editMode")).SelectedValue)=="Toggle","Mute selects toggle automatically");
  Check((bool)Call(form,"ApplyInspector",false)&&mappings[0].Mode=="Toggle","Mute draft validation");
  target.SelectedValue="Gain";Check((decimal)((NumericUpDown)Get(form,"editMin")).Value==-12&&((NumericUpDown)Get(form,"editMax")).Value==30,"Gain defaults use physical preamp range");
  Check((bool)Call(form,"ApplyInspector",false)&&mappings[0].Target=="Gain","Gain inspector stores compatible profile fields");
  form.Size=new Size(1180,780);Application.DoEvents();Check(((Panel)Get(form,"controllerPage")).ClientSize.Width>1000,"Minimum window width usable");
  Call(form,"ShowPage",false);Call(form,"DirectLevel",0,.6);Call(form,"RefreshVisuals",true);Application.DoEvents();
  var strips=(MixerStrip[])Get(form,"strips");Check(strips[0].AccessibleDescription.Contains("dB"),"Fader accessibility reports actual state");
  Call(form,"ShowPage",true);var editName=(TextBox)Get(form,"editName");editName.Text="Микшер";
  var rawDevice=(ComboBox)Get(form,"devices");if(rawDevice.Items.Count==0){rawDevice.Items.Add("Устройство");rawDevice.SelectedIndex=0;}string deviceBefore=rawDevice.Text;
  string mappingBefore=mappings[0].Name;var mode=(ComboBox)Get(form,"editMode");string modeKey=Convert.ToString(mode.SelectedValue);
  Call(form,"SetLanguage",true,false);Application.DoEvents();
  Check(((Button)Get(form,"mixerTab")).Text=="Mixer"&&((Button)Get(form,"controllerTab")).Text=="Controllers","English navigation");
  Check(arm.Text=="TEST MODE","English dynamic status");
  Check(editName.Text=="Микшер"&&mappings[0].Name==mappingBefore,"Language preserves draft and user mapping names");
  Check(rawDevice.Text==deviceBefore,"Language preserves MIDI device names");
  Check(Convert.ToString(mode.SelectedValue)==modeKey,"Language preserves protocol mode key");
  Check(gridColumn(form,"Name")=="Control","English mapping column");
  Check(L.T("Неизвестный тип MIDI.")=="Unknown MIDI type.","English validation messages");
  Check(!arm.Checked,"Language switch does not enable live");
  Call(form,"SetLanguage",false,false);Application.DoEvents();
  Check(((Button)Get(form,"mixerTab")).Text=="Микшер"&&arm.Text=="ТЕСТОВЫЙ РЕЖИМ","Russian round trip");
  Check(editName.Text=="Микшер"&&Convert.ToString(mode.SelectedValue)==modeKey,"Round trip preserves pending edits");
  form.Close();
 }Console.WriteLine("TOTAL UI: "+checks+" passed. No real mixer was connected.");}
 static string gridColumn(MainForm form,string prop){var grid=(DataGridView)Get(form,"grid");foreach(DataGridViewColumn c in grid.Columns)if(c.DataPropertyName==prop)return c.HeaderText;return null;}
}
}
