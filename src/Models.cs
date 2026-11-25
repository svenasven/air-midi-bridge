using System;
using System.Text.RegularExpressions;
namespace MR18Midi {
public sealed class MixerModel {
 public string Name;public int Channels,Preamps;
 public static MixerModel Find(string name){
  switch(name){
   case "MR18":case "MR18V2":case "XR18":case "X18":return new MixerModel{Name=name,Channels=16,Preamps=16};
   case "MR12":case "MR12W":case "XR12":return new MixerModel{Name=name,Channels=12,Preamps=4};
   case "XR16":return new MixerModel{Name=name,Channels=16,Preamps=8};
   default:return null;
  }
 }
 public bool Supports(string path){
  var m=Regex.Match(path,@"^/(ch|headamp)/([0-9]{2})/");
  if(m.Success){int n=int.Parse(m.Groups[2].Value);return n>=1&&n<=(m.Groups[1].Value=="headamp"?Preamps:Channels);}
  m=Regex.Match(path,@"^/-stat/solosw/([0-9]{2})$");if(m.Success){int n=int.Parse(m.Groups[1].Value);return n>=1&&n<=Channels;}
  return path=="/lr/mix/fader";
 }
}
}
