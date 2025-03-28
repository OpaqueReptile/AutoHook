using System;
using System.Collections.Generic;
using System.ComponentModel;
using AutoHook.Classes;
using AutoHook.Data;
using AutoHook.Enums;
using AutoHook.Fishing;
using AutoHook.Resources.Localization;
using AutoHook.Utils;

namespace AutoHook.Configurations;

public class HookConfig : BaseOption
{
    [DefaultValue(true)] public bool Enabled = true;

    public BaitFishClass BaitFish = new();

    public BaseHookset NormalHook = new(IDs.Status.None);
    public BaseHookset IntuitionHook = new(IDs.Status.FishersIntuition);

    //todo enable more hook settings based on the current status
    //List<BaseHookset> CustomHooksets = new();

    public HookConfig()
    {
    }

    public HookConfig(BaitFishClass baitFish)
    {
        BaitFish = baitFish;
    }

    public HookConfig(int baitFishId)
    {
        BaitFish = new BaitFishClass(baitFishId);
    }

    public void SetBiteAndHookType(BiteType bite, HookType hookType, bool isIntuition = false)
    {
        BaseHookset hookset = isIntuition ? IntuitionHook : NormalHook;
        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        if (hookDictionary.TryGetValue(bite, out var hook))
        {
            hook.ph.HooksetEnabled = true;
            hook.ph.HooksetType = hookType;

            hook.dh.HooksetEnabled = true;
            hook.th.HooksetEnabled = true;
        }
    }

    public void SetHooksetTimer(BiteType bite, double min, double max, bool isIntuition = false)
    {
        BaseHookset hookset = isIntuition ? IntuitionHook : NormalHook;
        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        if (hookDictionary.TryGetValue(bite, out var hook))
        {
            hook.ph.MinHookTimer = min;
            hook.ph.MaxHookTimer = max + 1;
            hook.ph.HookTimerEnabled = true;

            hook.dh.MinHookTimer = min;
            hook.dh.MaxHookTimer = max + 1;
            hook.dh.HookTimerEnabled = true;

            hook.th.MinHookTimer = min;
            hook.th.MaxHookTimer = max + 1;
            hook.th.HookTimerEnabled = true;
        }
    }

    public void ResetAllHooksets()
    {
        ResetHooksets(NormalHook);
        ResetHooksets(IntuitionHook);
    }

    private void ResetHooksets(BaseHookset hookset)
    {
        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        foreach (var hookDisable in hookDictionary)
        {
            hookDisable.Value.ph.HooksetEnabled = false;
            hookDisable.Value.dh.HooksetEnabled = false;
            hookDisable.Value.th.HooksetEnabled = false;
        }
    }

    public BaseHookset GetHookset()
    {
        /*
            var requiredStatusPreset = new List<BaseHookset> { IntuitionHook };

            foreach (var preset in requiredStatusPreset)
            {
                if (PlayerRes.HasStatus(preset.RequiredStatus) && preset.UseCustomStatusHook)
                {
                    return preset;
                }
            }*/

        if (FishingManager.IntuitionStatus == IntuitionStatus.Active && IntuitionHook.UseCustomStatusHook)
        {
            return IntuitionHook;
        }

        return NormalHook;
    }

    public HookType? GetHook(BiteType bite, double timePassed)
    {
        var hookset = GetHookset();

        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };
        
        Service.Status = "";

        if (hookDictionary.TryGetValue(bite, out var hook))
        {
            // Triple Hook
            if (hookset.UseTripleHook && hook.th.HooksetEnabled)
            {
                if (CheckHookCondition(hook.th, timePassed))
                    if (IsHookAvailable(hook.th))
                        return hook.th.HooksetType;

                if (hookset.LetFishEscapeTripleHook && PlayerRes.GetCurrentGp() < 700)
                {
                    Service.Status = "Not enough GP to use Triple Hook, Letting fish escape is enabled";
                    return HookType.None;
                }

                Service.Status = $"(Triple Hook) {Service.Status}";
            }

            // Double Hook
            if (hookset.UseDoubleHook && hook.dh.HooksetEnabled)
            {
                if (CheckHookCondition(hook.dh, timePassed))
                    if (IsHookAvailable(hook.dh))
                        return hook.dh.HooksetType;

                if (hookset.LetFishEscapeDoubleHook && PlayerRes.GetCurrentGp() < 400)
                {
                    Service.Status = "Not enough GP to use Double Hook, Letting fish escape is enabled";
                    return HookType.None;
                }
                
                Service.Status = $"(Triple Hook) {Service.Status}";
            }

            // Normal - Patience
            if (hook.ph.HooksetEnabled)
            {
                if (CheckHookCondition(hook.ph, timePassed))
                    return IsHookAvailable(hook.ph) ? hook.ph.HooksetType : HookType.Normal;
                
                Service.Status = $"(Normal/Patience Hook) {Service.Status}";
            }
            else if (Service.Status == "")
                Service.Status = UIStrings.Status_NoHookEnabled;
        }

        //Service.Status = "Skipping bite - No hook for this bite is enabled";
        return HookType.None;
    }

    private bool CheckHookCondition(BaseBiteConfig hookType, double timePassed)
    {
        if (!CheckIdenticalCast(hookType))
            return false;

        if (!CheckSurfaceSlap(hookType))
            return false;
        
        if (!CheckPrizeCatch(hookType))
            return false;

        if (!CheckTimer(hookType, timePassed))
            return false;

        return true;
    }

    private bool IsHookAvailable(BaseBiteConfig hookType)
    {
        if (!PlayerRes.ActionTypeAvailable((uint)hookType.HooksetType))
        {
            Service.Status = $"Not available. Normal hook will be used instead";
            return false;
        }

        return true;
    }

    private bool CheckIdenticalCast(BaseBiteConfig hookType)
    {
        if (hookType.OnlyWhenActiveIdentical && !PlayerRes.HasStatus(IDs.Status.IdenticalCast))
        {
            Service.Status = UIStrings.Status_IdenticalCastRequired;
            return false;
        }

        if (hookType.OnlyWhenNotActiveIdentical && PlayerRes.HasStatus(IDs.Status.IdenticalCast))
        {
            Service.Status = UIStrings.Status_IdenticalCastNotRequired;
            return false;
        }

        return true;
    }

    private bool CheckPrizeCatch(BaseBiteConfig hookType)
    {
        if (hookType.PrizeCatchReq && !PlayerRes.HasStatus(IDs.Status.PrizeCatch))
        {
            Service.Status = UIStrings.Status_PrizeCatchRequired;
            return false;
        }

        if (hookType.PrizeCatchNotReq && PlayerRes.HasStatus(IDs.Status.PrizeCatch))
        {
            Service.Status = UIStrings.Status_PrizeCatchNotRequired;
        }
        
        return true;
    }

    private bool CheckSurfaceSlap(BaseBiteConfig hookType)
    {
        if (hookType.OnlyWhenActiveSlap && !PlayerRes.HasStatus(IDs.Status.SurfaceSlap))
        {
            Service.Status = UIStrings.Status_SurfaceSlapRequired;
            return false;
        }

        if (hookType.OnlyWhenNotActiveSlap && PlayerRes.HasStatus(IDs.Status.SurfaceSlap))
        {
            Service.Status = UIStrings.Status_SurfaceSlapNotRequired;
            return false;
        }

        return true;
    }

    private bool CheckTimer(BaseBiteConfig hookType, double timePassed)
    {
        double minimumTime = 0;
        double maximumTime = 0;

        if (PlayerRes.HasStatus(IDs.Status.Chum))
        {
            if (hookType.ChumTimerEnabled)
            {
                minimumTime = hookType.ChumMinHookTimer;
                maximumTime = hookType.ChumMaxHookTimer;
            }
        }
        else if (hookType.HookTimerEnabled)
        {
            minimumTime = hookType.MinHookTimer;
            maximumTime = hookType.MaxHookTimer;
        }

        if (minimumTime > 0 && timePassed < minimumTime)
        {
            Service.Status = $"Skipping bite - Minimum time has not been met - Current: {timePassed} < Min: {minimumTime}";
            return false;
        }

        if (maximumTime > 0 && timePassed > maximumTime)
        {
            Service.Status = $"Skipping bite - Maximum time has been exceeded - Current: {timePassed} > Max: {maximumTime}";
            return false;
        }

        return true;
    }

    public override void DrawOptions()
    {
    }

    public override bool Equals(object? obj)
    {
        return obj is HookConfig settings &&
               BaitFish == settings.BaitFish;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UniqueId);
    }
}