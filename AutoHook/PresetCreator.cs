﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoHook.Classes;
using AutoHook.Configurations;
using AutoHook.Data;
using AutoHook.Enums;
using AutoHook.Resources.Localization;
using AutoHook.Ui;
using AutoHook.Utils;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace AutoHook;

// ReSharper disable LocalizableElement
public class PresetCreator
{
    private string _newPresetName = "";
    private Fish? _selectedTargetFish;
    private List<Fish> _presetMoochList = [];
    private List<(Fish, int)> _presetPrepList = [];
    private bool _includeTimers;
    private bool _includeIntPrep;
    private bool _fishEyes;
    private bool _createAnglersPreset;

    private void DrawHeader()
    {
        ImGui.PushTextWrapPos();
        ImGui.TextColored(ImGuiColors.DalamudYellow,
            "!!! Experimental Feature !!! \nThis is not optimized at the moment and its just a starting point\nJoin the discord and leave a suggestion on how to improve");
        ImGui.PopTextWrapPos();

        DrawUtil.TextV("Selected the target fish");
        DrawUtil.DrawComboSelector(
            GameRes.ImportedFishes.Where(f => !f.IsSpearFish).ToList(),
            item => item.Name,
            _selectedTargetFish?.Name ?? UIStrings.None,
            item => SetSelectedFish(item));

        DrawUtil.TextV("Preset Name: ");
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("###input", $"Auto - {_selectedTargetFish?.Name ?? "Preset Name"}",
                ref _newPresetName, 64, ImGuiInputTextFlags.AutoSelectAll))
        {
        }
    }

    private void SetSelectedFish(Fish fish)
    {
        _selectedTargetFish = fish;
        _presetMoochList = [];
        _presetPrepList = [];
    }

    public void DrawPresetGenerator()
    {
        try
        {
            DrawHeader();

            if (_selectedTargetFish == null)
                return;

            DrawUtil.SpacingSeparator();
            ImGui.TextWrapped(
                $"Initial Bait: {MultiString.GetItemName(_selectedTargetFish.InitialBait)}");
            
            if (_selectedTargetFish.Mooches.Count > 0)
            {
                if (_presetMoochList.Count == 0)
                {
                    _presetMoochList = _selectedTargetFish.Mooches
                        .Select(mooch => GameRes.ImportedFishes.FirstOrDefault(f => f.ItemId == mooch)).OfType<Fish>()
                        .ToList();
                }
                
                DrawUtil.TextV(
                    $"Mooch order: {string.Join(" > ", _presetMoochList.Select(fish => $"{fish.Name} {GetBiteType(fish.BiteType)}"))}");
            }

            DrawUtil.Checkbox("Include fish hooking timers", ref _includeTimers,
                "The values are based on the info available on TeamCraft and are not 100% accurate");

            if (_selectedTargetFish.Predators.Count > 0)
            {
                DrawUtil.Checkbox("Include intuition preparation in the same preset > READ", ref _includeIntPrep,
                    "Even more experimental, works well with 1 fish requirement but 2 or more idk about that (will be improved)");
                
                if (_presetPrepList.Count == 0)
                {
                    foreach (var predator in _selectedTargetFish.Predators)
                    {
                        var fish = GameRes.ImportedFishes.FirstOrDefault(f => f.ItemId == predator.itemId);

                        if (fish != null)
                            _presetPrepList.Add((fish, predator.qtd));
                    }
                }
                
                if (_includeIntPrep)
                {
                    DrawUtil.TextV($"Intuition Prep:\n{string.Join("\n", _presetPrepList.Select(fish
                        => $"{fish.Item2}x {fish.Item1.Name} {GetBiteType(fish.Item1.BiteType)} ({MultiString.GetItemName(fish.Item1.InitialBait)})"))}");
                }
            }

            DrawUtil.Checkbox("Setup Auto Casting for Fish Eyes", ref _fishEyes,
                "This is a simple setup, useful for catching old expansions big fishes");

            if (_fishEyes)
            {
                ImGui.Indent();
                ImGui.PushTextWrapPos();

                if (_presetMoochList.Count > 0)
                {
                    ImGui.TextColored(ImGuiColors.DalamudYellow,
                        "Since this fish requires mooching, its recommended to start with 10 Anglers Art for Makeshift Bait.");

                    DrawUtil.Checkbox("Create a Anglers Art stacking preset (versatile lure)",
                        ref _createAnglersPreset);
                }

                ImGui.PopTextWrapPos();
                ImGui.Unindent();
            }

            if (ImGui.Button("Create Preset and Close"))
            {
                GeneratePreset(_presetMoochList, _presetPrepList);
            }
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e.Message);
        }
    }

    private void GeneratePreset(List<Fish> moochList, List<(Fish, int)> prepList)
    {
        if (_selectedTargetFish == null)
            return;

        var isInt = prepList.Count > 0;

        if (_newPresetName == string.Empty)
            _newPresetName = $"Auto - {_selectedTargetFish.Name} {DateTime.Now}";

        var newPreset = new PresetConfig(_newPresetName);

        SetupBaitAndMooch(newPreset, _selectedTargetFish.InitialBait, _selectedTargetFish, moochList, isInt);

        newPreset.ExtraCfg.Enabled = true;
        newPreset.ExtraCfg.ForceBaitSwap = true;
        newPreset.ExtraCfg.ForcedBaitId = _selectedTargetFish!.InitialBait;
        
        if (_includeIntPrep)
            SetupIntPrep(newPreset);
        
        if (_fishEyes)
            SetupFishEyes(newPreset);
        else
        {
            newPreset.AutoCastsCfg.EnableAll = true;
            newPreset.AutoCastsCfg.CastLine.Enabled = true;
            newPreset.AutoCastsCfg.CastMooch.Enabled = true;
        }

        newPreset.AddFishConfig(new FishConfig(_selectedTargetFish.ItemId) { StopAfterCaught = true });
        
        if (_createAnglersPreset)
        {
            var anglers = CreateAnglerPreset();
            anglers.ExtraCfg.AnglerStackQtd = 10;
            anglers.ExtraCfg.SwapBaitAnglersArt = true;
            anglers.ExtraCfg.BaitToSwapAnglersArt = new BaitFishClass(newPreset.ExtraCfg.ForcedBaitId);
            
            anglers.ExtraCfg.SwapPresetAnglersArt = true;
            anglers.ExtraCfg.PresetToSwapAnglersArt = newPreset.PresetName;
            Service.Configuration.HookPresets.CustomPresets.Add(anglers);
        }
        
        Service.Configuration.HookPresets.CustomPresets.Add(newPreset);
        Service.Configuration.HookPresets.SelectedPreset = newPreset;

        ResetOptions();

        Service.Save();

        TabCustomPresets.OpenPresetGen = false;
    }

    private void SetupFishEyes(PresetConfig newPreset)
    {
        if (_selectedTargetFish == null)
            return;
        
        newPreset.AutoCastsCfg.EnableAll = true;
        newPreset.AutoCastsCfg.CastLine.Enabled = true;
        newPreset.AutoCastsCfg.CastLine.OnlyCastWithFishEyes = true;
        newPreset.AutoCastsCfg.CastCordial.Enabled = true;
        newPreset.AutoCastsCfg.CastFishEyes.Enabled = true;
        newPreset.AutoCastsCfg.CastFishEyes.IgnoreMooch = true;

        if (_selectedTargetFish!.Mooches.Count > 0)
        {
            newPreset.AutoCastsCfg.CastFishEyes.OnlyWhenMakeShiftUp = true;
            newPreset.AutoCastsCfg.CastPatience.Enabled = true;
            newPreset.AutoCastsCfg.CastPatience.Id = IDs.Actions.Patience;
            newPreset.AutoCastsCfg.CastPatience.GpThreshold = 770;
            newPreset.AutoCastsCfg.CastMakeShiftBait.Enabled = true;
        }
    }

    private void SetupIntPrep(PresetConfig newPreset)
    {
        foreach (var fishPrep in _presetPrepList)
        {
            var fish = fishPrep.Item1;
            var mooches = fish.Mooches
                .Select(mooch => GameRes.ImportedFishes.FirstOrDefault(f => f.ItemId == mooch)).OfType<Fish>()
                .ToList();

            SetupBaitAndMooch(newPreset, fish.InitialBait, fish, mooches);
            var fishConfig = new FishConfig(fishPrep.Item1.ItemId);
            fishConfig.IgnoreOnIntuition = true;
            
            newPreset.ExtraCfg.ForcedBaitId = fish.InitialBait;
            newPreset.AddFishConfig(fishConfig);
        }
    }

    private void SetupBaitAndMooch(PresetConfig newPreset, int bait, Fish fishTarget, List<Fish>? moochList,
        bool isIntuition = false)
    {
        var initialBaitCfg = newPreset.ListOfBaits.FirstOrDefault(f => f.BaitFish.Id == bait);

        if (initialBaitCfg == null)
        {
            initialBaitCfg = new HookConfig(bait);
            initialBaitCfg.ResetAllHooksets();
        }

        if (isIntuition)
            initialBaitCfg.IntuitionHook.UseCustomStatusHook = true;

        // if theres no mooch, set the bait to hook the Tug from the target fish
        if (moochList == null || moochList.Count == 0)
        {
            initialBaitCfg.SetBiteAndHookType(fishTarget.BiteType, fishTarget!.HookType, isIntuition);

            if (_includeTimers)
            {
                var timer = GameRes.BiteTimers.FirstOrDefault(b => b.itemId == fishTarget.ItemId) ?? new BiteTimers();
                initialBaitCfg.SetHooksetTimer(fishTarget.BiteType, timer.min, timer.max, isIntuition);
            }

            newPreset.ReplaceBaitConfig(initialBaitCfg);
            return;
        }

        // the list is going backwards to make it easier
        moochList.Reverse();

        foreach (var mooch in moochList)
        {
            // check if the mooch is already included in the list
            var newMooch = newPreset.ListOfMooch.FirstOrDefault(f => f.BaitFish.Id == mooch.ItemId);

            if (newMooch == null)
            {
                newMooch = new HookConfig(mooch.ItemId);
                newMooch.ResetAllHooksets();
            }

            if (isIntuition)
                newMooch.IntuitionHook.UseCustomStatusHook = true;

            // Add the fish to the Fish Caught tab and enable Auto Mooch I/II
            var fishConfig = new FishConfig(mooch.ItemId);
            fishConfig.Mooch.Enabled = true;
            fishConfig.Mooch.Mooch2.Enabled = true;
            newPreset.AddFishConfig(fishConfig);

            Fish nextFish;

            // target fish < last mooch < other mooches < first mooch < bait
            // in other words, the bait needs to know the BiteType of the first mooch and the last mooch needs to know the bite of the target fish
            // The list is reversed so we can setup more easily
            if (mooch == moochList.First())
                nextFish = fishTarget;
            else if (mooch == moochList.Last())
                nextFish = moochList[^2];
            else
                nextFish = moochList[moochList.IndexOf(mooch) - 1];

            // only hook the next fish BiteType
            // REMEMBER YOU FUCK, THE NEXT FISH IS THE PREVIOUS ONE IN THE LIST
            newMooch.SetBiteAndHookType(nextFish.BiteType, nextFish.HookType, isIntuition);

            if (_includeTimers)
            {
                var timer = GameRes.BiteTimers.FirstOrDefault(b => b.itemId == nextFish.ItemId) ?? new BiteTimers();
                newMooch.SetHooksetTimer(nextFish.BiteType, timer.min, timer.max, isIntuition);
            }

            newPreset.ReplaceMoochConfig(newMooch);

            // the last fish in the list is the first one being hooked
            if (mooch == moochList.Last())
            {
                // that means we need to set up the bait to the this fish bite.
                initialBaitCfg.SetBiteAndHookType(mooch.BiteType, mooch.HookType, isIntuition);
                if (_includeTimers)
                {
                    var timer = GameRes.BiteTimers.FirstOrDefault(b => b.itemId == mooch.ItemId) ?? new BiteTimers();
                    initialBaitCfg.SetHooksetTimer(mooch.BiteType, timer.min, timer.max, isIntuition);
                }

                newPreset.ReplaceBaitConfig(initialBaitCfg);
            }
        }
    }


    private PresetConfig CreateAnglerPreset()
    {
        PresetConfig anglers = new($"Auto -  StackAngler {DateTime.Now}");

        var bait = new HookConfig(29717); // versatile lure
        
        anglers.ExtraCfg.Enabled = true;
        anglers.ExtraCfg.ForceBaitSwap = true;
        anglers.ExtraCfg.ForcedBaitId = 29717;
        
        anglers.AutoCastsCfg.EnableAll = true;
        anglers.AutoCastsCfg.CastLine.Enabled = true;
        anglers.AutoCastsCfg.CastPatience.Enabled = true;
        anglers.AutoCastsCfg.CastCordial.Enabled = true;
        anglers.AutoCastsCfg.DontCancelMooch = false;

        anglers.AddBaitConfig(bait);

        return anglers;
    }

    private PresetConfig CreateAnglerPresetTest()
    {
        PresetConfig anglers = new($"Auto - 600gp StackAngler {DateTime.Now}");

        var bait = new HookConfig(29717); // versatile lure
        bait.NormalHook.UseDoubleHook = true;
        bait.NormalHook.UseTripleHook = true;

        anglers.ExtraCfg.Enabled = true;
        anglers.ExtraCfg.ForceBaitSwap = true;
        anglers.ExtraCfg.ForcedBaitId = 29717;

        anglers.AutoCastsCfg.EnableAll = true;
        anglers.AutoCastsCfg.CastCordial.Enabled = true;
        anglers.AutoCastsCfg.CastPrizeCatch.Enabled = true;
        anglers.AutoCastsCfg.CastPrizeCatch.GpThreshold = 600;
        anglers.AutoCastsCfg.CastLine.Enabled = true;
        anglers.AutoCastsCfg.DontCancelMooch = false;


        anglers.AddBaitConfig(bait);

        return anglers;
    }

    private static string GetBiteType(BiteType bite)
        => bite switch
        {
            BiteType.Weak => "(!)",
            BiteType.Strong => "(!!)",
            BiteType.Legendary => "(!!!)",
            _ => "Error",
        };

    private void ResetOptions()
    {
        _newPresetName = "";
        _selectedTargetFish = null;

        _includeTimers = false;

        _includeIntPrep = false;
        _fishEyes = false;
        _createAnglersPreset = false;
    }
}