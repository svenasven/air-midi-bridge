using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MR18Midi {
public partial class MainForm {
 FlagButton russianLanguage,englishLanguage;
 Control BuildLanguageButtons(){
  var panel=new FlowLayoutPanel{Width=152,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(2,2,4,0)};
  russianLanguage=new FlagButton{IsEnglish=false,AccessibleName="Русский / Russian"};englishLanguage=new FlagButton{IsEnglish=true,AccessibleName="English / Английский"};
  russianLanguage.Click+=(s,e)=>SetLanguage(false,true);englishLanguage.Click+=(s,e)=>SetLanguage(true,true);
  panel.Controls.Add(russianLanguage);panel.Controls.Add(englishLanguage);return panel;
 }
 void SetLanguage(bool english,bool persist){L.English=english;ApplyLanguage();if(persist)try{L.Save();}catch(Exception e){Write(L.T("Ошибка: ")+e.Message);}}
 void ApplyLanguage(){
  // Do not rebind the mapping list or reload the inspector: those operations
  // could discard an unsaved draft or send an accidental control change.
  bool previous=loadingInspector;loadingInspector=true;SuspendLayout();
  try{
   TranslateTree(this);
   foreach(DataGridViewColumn column in grid.Columns)column.HeaderText=L.Known(column.HeaderText);
   if(russianLanguage!=null){russianLanguage.Selected=!L.English;englishLanguage.Selected=L.English;russianLanguage.Invalidate();englishLanguage.Invalidate();}
   shownProfile=null;RefreshVisuals(true);foreach(var strip in strips)if(strip!=null)strip.Invalidate();grid.Invalidate();
  }finally{loadingInspector=previous;ResumeLayout(true);}
 }
 void TranslateTree(Control control){
  var choiceBox=control as ComboBox;
  if(choiceBox!=null){
   var list=choiceBox.DataSource as List<Choice>;if(list!=null){object key=choiceBox.SelectedValue;foreach(var item in list)item.Title=L.Known(item.Title);var manager=BindingContext[list] as CurrencyManager;if(manager!=null)manager.Refresh();if(key!=null)choiceBox.SelectedValue=key;}
   // Never translate a MIDI device name, even if it resembles a resource key.
  }else if(!(control is TextBox)&&!(control is NumericUpDown)&&!(control is FlagButton)&&control!=inspectorTitle&&control!=profileLabel&&control!=learnStatus){
   control.Text=L.Known(control.Text);
  }
  if(!(control is FlagButton)&&control.AccessibleName!=null)control.AccessibleName=L.Known(control.AccessibleName);
  foreach(Control child in control.Controls)TranslateTree(child);
 }
}
public class FlagButton : Button {
 public bool IsEnglish,Selected;
 public FlagButton(){Size=new Size(70,36);Margin=new Padding(2,0,2,0);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Theme.Bg;ForeColor=Theme.Text;Cursor=Cursors.Hand;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint,true);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.Clear(Selected?Color.FromArgb(19,64,76):Theme.Bg);
  using(var p=new Pen(Selected?Theme.Cyan:Theme.Border))g.DrawRectangle(p,0,0,Width-1,Height-1);
  int x=8,y=10,w=27,h=16;
  if(!IsEnglish){using(var b=new SolidBrush(Color.White))g.FillRectangle(b,x,y,w,6);using(var b=new SolidBrush(Color.FromArgb(31,76,178)))g.FillRectangle(b,x,y+6,w,5);using(var b=new SolidBrush(Color.FromArgb(211,45,56)))g.FillRectangle(b,x,y+11,w,5);}
  else{
   g.FillRectangle(Brushes.White,x,y,w,h);
   using(var b=new SolidBrush(Color.FromArgb(193,47,65)))for(int i=0;i<13;i+=2)g.FillRectangle(b,x,y+i*h/13f,w,h/13f+.1f);
   using(var b=new SolidBrush(Color.FromArgb(46,59,110)))g.FillRectangle(b,x,y,13,9);
   for(int row=0;row<3;row++)for(int col=0;col<5;col++)g.FillRectangle(Brushes.White,x+1+col*2.4f,y+1+row*2.5f,.8f,.8f);
  }
  using(var font=new Font("Segoe UI",9,FontStyle.Bold))TextRenderer.DrawText(g,IsEnglish?"EN":"RU",font,new Rectangle(39,1,30,Height-2),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
  if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6),Theme.Cyan,BackColor);
 }
}
}
