using System;
using TitaniumLauncher.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace TitaniumLauncher.Services;
public sealed record MatchOptions(int MinimumBots, int MaximumBots, bool EditOnRelease)
{
    public BotPreset Difficulty {get;init;}=BotPreset.Builtin(2);
    public static MatchOptions Parse(string? text, bool editOnRelease)
    {
        text=(text??"").Trim().Replace('\u2013','-').Replace('\u2014','-');
        if(text.Length==0)return new(99,99,editOnRelease);
        var m=Regex.Match(text,@"^([0-9]{1,2})(?:\s*-\s*([0-9]{1,2}))?$");
        if(!m.Success)throw new InvalidOperationException("Enter a bot count from 0 to 99, or a range such as 40-60. Leave blank for 99.");
        int low=int.Parse(m.Groups[1].Value),high=m.Groups[2].Success?int.Parse(m.Groups[2].Value):low;
        if(low>high)throw new InvalidOperationException("The minimum bot count must not exceed the maximum.");
        return new(low,high,editOnRelease);
    }
    public void Apply(ProcessStartInfo info, bool host)
    {
        if(MinimumBots<0||MaximumBots>99||MinimumBots>MaximumBots)throw new InvalidOperationException("Invalid bot count range.");
        if(host){info.ArgumentList.Add("-TitaniumDifficulty="+Difficulty.Argument());info.ArgumentList.Add("-TitaniumBotsMin="+MinimumBots);info.ArgumentList.Add("-TitaniumBotsMax="+MaximumBots);}
        else info.ArgumentList.Add("-TitaniumEditOnRelease="+(EditOnRelease?"1":"0"));
    }
}
