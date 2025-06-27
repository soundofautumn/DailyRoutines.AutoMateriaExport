using System.Collections.Generic;
using DailyRoutines.Abstracts;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;

namespace DailyRoutines.AutoMateriaExport;

public class AutoMateriaExport : DailyModuleBase
{
    public override ModuleInfo Info { get; } = new()
    {
        Title       = GetLoc("AutoMateriaExport"),
        Description = GetLoc("AutoMateriaExportDescription"),
        Category    = ModuleCategories.Action,
    };

    public override void Init()
    {
        TaskHelper ??= new TaskHelper { TimeLimitMS = 30_000 };

        DService.ClientState.TerritoryChanged += OnZoneChanged;
        DService.DutyState.DutyRecommenced    += OnDutyRecommenced;
        DService.Condition.ConditionChange    += OnConditionChanged;
    }

    // 重新挑战
    private void OnDutyRecommenced(object? sender, ushort e)
    {
        TaskHelper.Abort();
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    // 进入副本
    private void OnZoneChanged(ushort zone)
    {
        if (LuminaGetter.GetRow<TerritoryType>(zone) is not { ContentFinderCondition.RowId: > 0 }) return;

        TaskHelper.Abort();
        TaskHelper.Enqueue(CheckCurrentJob);
    }
    
    // 战斗状态
    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is not ConditionFlag.InCombat) return;
        
        TaskHelper.Abort();
        if (!value) 
            TaskHelper.Enqueue(CheckCurrentJob);
    }

    private bool? CheckCurrentJob()
    {
        if (BetweenAreas || !IsScreenReady() || OccupiedInEvent) return false;
        if (DService.Condition[ConditionFlag.InCombat] || 
            DService.ClientState.LocalPlayer is not { ClassJob.RowId: 39 } || !IsValidPVEDuty())
        {
            TaskHelper.Abort();
            return true;
        }
        
        TaskHelper.Enqueue(UseRelatedActions, "UseRelatedActions", 5_000, true, 1);
        return true;
    }
    
    private unsafe bool? UseRelatedActions()
    {
        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null) return null;

        var statusManager = localPlayer->StatusManager;

        // 播魂种
        if (statusManager.HasStatus(2594) || !IsActionUnlocked(24387))
        {
            TaskHelper.Abort();
            return true;
        }

        TaskHelper.Enqueue(() => UseActionManager.UseAction(ActionType.Action, 24387), $"UseAction_{24387}",
                           5_000, true, 1);
        TaskHelper.DelayNext(2_000);
        TaskHelper.Enqueue(CheckCurrentJob, "SecondCheck", null, true, 1);
        return true;
    }

    private static unsafe bool IsValidPVEDuty()
    {
        HashSet<uint> InvalidContentTypes = [16, 17, 18, 19, 31, 32, 34, 35];

        var isPVP = GameMain.IsInPvPArea() || GameMain.IsInPvPInstance();

        var contentData = LuminaGetter.GetRow<ContentFinderCondition>(GameMain.Instance()->CurrentContentFinderConditionId);
        
        return !isPVP && (contentData == null || !InvalidContentTypes.Contains(contentData.Value.ContentType.RowId));
    }

    public override void Uninit()
    {
        DService.ClientState.TerritoryChanged -= OnZoneChanged;
        DService.DutyState.DutyRecommenced    -= OnDutyRecommenced;
        DService.Condition.ConditionChange    -= OnConditionChanged;

        base.Uninit();
    }
}
