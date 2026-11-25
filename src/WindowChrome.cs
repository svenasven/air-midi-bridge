using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MR18Midi {
public partial class MainForm {
 Button minimizeWindow,maximizeWindow,closeWindow;
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ApplyDarkFrame();}
 void ApplyDarkFrame(){
  // Unsupported DWM attributes return an HRESULT on older Windows releases.
  try{int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,4);int none=unchecked((int)0xFFFFFFFE);DwmSetWindowAttribute(Handle,34,ref none,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
 }
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr handle,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
 [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo {public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
 protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.Style|=0x00040000|0x00020000|0x00010000|0x00080000;return cp;}}
 void SetupChrome(){
  FormBorderStyle=FormBorderStyle.None;Padding=new Padding(6);BackColor=Theme.Bg;
 }
 Control BuildHeader(){
  var header=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Bg};
  var windowButtons=new FlowLayoutPanel{Dock=DockStyle.Right,Width=144,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(0,2,0,0),Margin=Padding.Empty};
  minimizeWindow=CaptionButton("−",L.T("Свернуть"),()=>WindowState=FormWindowState.Minimized);
  maximizeWindow=CaptionButton("□",L.T("Развернуть / восстановить"),ToggleMaximized);
  closeWindow=CaptionButton("×",L.T("Закрыть"),Close);closeWindow.FlatAppearance.MouseOverBackColor=Color.FromArgb(166,45,56);
  windowButtons.Controls.AddRange(new Control[]{minimizeWindow,maximizeWindow,closeWindow});
  var content=new FlowLayoutPanel{Dock=DockStyle.Left,Width=620,WrapContents=false,BackColor=Theme.Bg};
  title=Theme.Label("AIR MIDI Bridge",21,true);title.Margin=new Padding(2,4,32,4);content.Controls.Add(title);
  mixerTab=Theme.Button(L.T("Микшер"),()=>ShowPage(false));controllerTab=Theme.Button(L.T("Контроллеры"),()=>ShowPage(true));content.Controls.Add(mixerTab);content.Controls.Add(controllerTab);var version=Theme.Label("1.1.0",9);version.ForeColor=Theme.Muted;content.Controls.Add(version);
  var languageButtons=BuildLanguageButtons();languageButtons.Dock=DockStyle.Right;header.Controls.Add(content);header.Controls.Add(languageButtons);header.Controls.Add(windowButtons);
  foreach(Control area in new Control[]{header,content,title,version}){
   area.MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;if(e.Clicks==2){ToggleMaximized();return;}ReleaseCapture();SendMessage(Handle,0x00A1,(IntPtr)2,IntPtr.Zero);};
  }
  return header;
 }
 Button CaptionButton(string glyph,string name,Action click){var b=new Button{Text=glyph,AccessibleName=name,Size=new Size(46,36),Margin=Padding.Empty,Padding=Padding.Empty,FlatStyle=FlatStyle.Flat,BackColor=Theme.Bg,ForeColor=Theme.Muted,Font=new Font("Segoe UI",15),TabStop=true,Cursor=Cursors.Hand};b.FlatAppearance.BorderSize=0;b.FlatAppearance.MouseOverBackColor=Theme.Panel;b.Click+=(s,e)=>click();return b;}
 void ToggleMaximized(){WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
 protected override void OnResize(EventArgs e){base.OnResize(e);if(maximizeWindow!=null)maximizeWindow.Text=WindowState==FormWindowState.Maximized?"❐":"□";}
 // A border remains outside the docked client content, so native hit testing
 // can resize each edge without stealing interaction from faders or buttons.
 internal static int ResizeHit(Point p,Size size,int grip){
  bool left=p.X>=0&&p.X<grip,right=p.X<size.Width&&p.X>=size.Width-grip;
  bool top=p.Y>=0&&p.Y<grip,bottom=p.Y<size.Height&&p.Y>=size.Height-grip;
  if(top&&left)return 13;if(top&&right)return 14;if(bottom&&left)return 16;if(bottom&&right)return 17;
  if(left)return 10;if(right)return 11;if(top)return 12;if(bottom)return 15;return 1;
 }
 protected override void WndProc(ref Message m){
  if(m.Msg==0x0085){m.Result=IntPtr.Zero;return;} // don't repaint legacy light non-client frame
  if(m.Msg==0x0086){m.LParam=(IntPtr)(-1);base.WndProc(ref m);return;} // activate without legacy border repaint
  if(m.Msg==0x0320||m.Msg==0x031E){base.WndProc(ref m);if(IsHandleCreated)ApplyDarkFrame();return;}
  if(m.Msg==0x0083){m.Result=IntPtr.Zero;return;} // client area occupies the native caption
  if(m.Msg==0x0024){
   base.WndProc(ref m);if(m.LParam!=IntPtr.Zero){
    var monitor=MonitorFromWindow(Handle,2);var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
    if(GetMonitorInfo(monitor,ref info)){var data=(MinMaxInfo)Marshal.PtrToStructure(m.LParam,typeof(MinMaxInfo));data.MaxPosition.X=info.Work.Left-info.Monitor.Left;data.MaxPosition.Y=info.Work.Top-info.Monitor.Top;data.MaxSize.X=info.Work.Right-info.Work.Left;data.MaxSize.Y=info.Work.Bottom-info.Work.Top;data.MinTrackSize.X=Math.Max(data.MinTrackSize.X,MinimumSize.Width);data.MinTrackSize.Y=Math.Max(data.MinTrackSize.Y,MinimumSize.Height);Marshal.StructureToPtr(data,m.LParam,false);}
   }return;
  }
  if(m.Msg==0x0084){base.WndProc(ref m);if(WindowState==FormWindowState.Normal){long packed=m.LParam.ToInt64();var point=PointToClient(new Point(unchecked((short)(packed&65535)),unchecked((short)((packed>>16)&65535))));int hit=ResizeHit(point,ClientSize,Padding.Left);if(hit!=1)m.Result=(IntPtr)hit;}return;}
  base.WndProc(ref m);
 }
}
}
