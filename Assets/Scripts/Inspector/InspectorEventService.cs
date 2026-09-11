using System;
using System.Collections.Generic;

/// <summary>감독관의 대사와 퇴장 확인 상태.</summary>
public enum InspectorEventPhase
{
    Dialogue,
    AwaitingExit,
    Completed,
    InspectorEventPhase_End
}

/// <summary>화면 수명과 무관하게 세션이 보관하는 감독관 진행의 읽기 전용 스냅샷.</summary>
public readonly struct InspectorEventSnapshot
{
    /// <summary>표시 일차. 늦게 도착한 전날 입력을 구분한다.</summary>
    public uint Day { get; }
    /// <summary>현재 이벤트 PK. 완료 상태에서는 0.</summary>
    public uint EventIdx { get; }
    /// <summary>현재 대사 인덱스(0부터).</summary>
    public int LineIndex { get; }
    /// <summary>현재 대사의 Text FK.</summary>
    public uint TextIdx { get; }
    /// <summary>현재 초상의 Resource FK.</summary>
    public uint PortraitResourceIdx { get; }
    /// <summary>대사·퇴장 대기·완료 상태.</summary>
    public InspectorEventPhase Phase { get; }

    /// <summary>서비스 내부에서 현재 표시값을 복사한다.</summary>
    /// <param name="day">표시 일차.</param>
    /// <param name="row">현재 이벤트 또는 null.</param>
    /// <param name="line">대사 위치.</param>
    /// <param name="phase">현재 상태.</param>
    internal InspectorEventSnapshot(uint day, InspectorEventData row, int line, InspectorEventPhase phase)
    {
        Day = day;
        EventIdx = row?.Idx ?? 0;
        LineIndex = line;
        TextIdx = row == null ? 0 : row.DialogueTextIdxs[line];
        PortraitResourceIdx = row?.PortraitResourceIdx ?? 0;
        Phase = phase;
    }
}

/// <summary>날짜별 선정(빈 결과 포함), 대사 진행과 완료 이력을 소유한다. 경제·손님 시스템을 변경하지 않는다.</summary>
public sealed class InspectorEventService
{
    private readonly List<InspectorEventData> rows = new List<InspectorEventData>();
    private readonly HashSet<uint> completedSession = new HashSet<uint>();
    private readonly HashSet<(uint day, uint idx)> completedDays = new HashSet<(uint, uint)>();
    private List<InspectorEventData> selected = new List<InspectorEventData>();
    private uint day;
    private int eventIndex;
    private int lineIndex;
    private InspectorEventPhase phase = InspectorEventPhase.Completed;

    /// <summary>현재 표시 상태. 화면을 재생성해도 같은 진행을 조회한다.</summary>
    public InspectorEventSnapshot Current => new InspectorEventSnapshot(day,
        eventIndex < selected.Count ? selected[eventIndex] : null, lineIndex, phase);
    /// <summary>퇴장까지 마치지 않은 이벤트가 있는지 여부.</summary>
    public bool HasPending => phase != InspectorEventPhase.Completed;

    /// <summary>검증된 행을 복사하여 외부 DTO 변경이 진행에 영향을 주지 않게 한다.</summary>
    /// <param name="source">검증된 감독관 이벤트.</param>
    /// <exception cref="ArgumentException">잘못된 행 또는 중복 PK.</exception>
    /// <exception cref="ArgumentNullException">입력이 null.</exception>
    public InspectorEventService(IEnumerable<InspectorEventData> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var ids = new HashSet<uint>();
        foreach (InspectorEventData row in source)
        {
            if (row == null) throw new ArgumentException("감독관 행 누락", nameof(source));
            row.Validate();
            if (!ids.Add(row.Idx)) throw new ArgumentException($"감독관 PK={row.Idx} 중복", nameof(source));
            rows.Add(new InspectorEventData
            {
                Idx = row.Idx, NameIdx = row.NameIdx, Day = row.Day,
                RequiredFacilityIdx = row.RequiredFacilityIdx, MinStoreStage = row.MinStoreStage,
                Priority = row.Priority, RepeatMode = row.RepeatMode,
                DialogueTextIdxs = (uint[])row.DialogueTextIdxs.Clone(), PortraitResourceIdx = row.PortraitResourceIdx
            });
        }
        rows.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Idx.CompareTo(b.Idx));
    }

    /// <summary>전날까지의 보유 스냅샷으로 하루에 한 번 선정한다. 같은 날의 재호출은 빈 결과도 유지한다.</summary>
    /// <param name="displayDay">1부터 시작하는 일차.</param>
    /// <param name="priorOwnedFacilities">전날까지 구매한 설비 PK.</param>
    /// <param name="priorStoreStage">전날까지 도달한 가게 단계.</param>
    /// <exception cref="ArgumentException">날짜·스냅샷 값이 잘못됨.</exception>
    /// <exception cref="InvalidOperationException">미완료 이벤트를 남기고 날짜를 변경함.</exception>
    public void BeginDay(uint displayDay, IReadOnlyCollection<uint> priorOwnedFacilities, uint priorStoreStage)
    {
        if (displayDay == 0 || displayDay < day) throw new ArgumentOutOfRangeException(nameof(displayDay));
        if (displayDay == day) return;
        if (HasPending) throw new InvalidOperationException("감독관 퇴장 완료 전에 다음 날을 시작할 수 없습니다.");
        if (priorOwnedFacilities == null) throw new ArgumentNullException(nameof(priorOwnedFacilities));
        if (priorStoreStage < 1 || priorStoreStage > 3) throw new ArgumentOutOfRangeException(nameof(priorStoreStage));
        var owned = new HashSet<uint>(priorOwnedFacilities);
        var candidates = new List<InspectorEventData>();
        foreach (InspectorEventData row in rows)
        {
            if (row.Day.HasValue && row.Day.Value != displayDay ||
                row.RequiredFacilityIdx.HasValue && !owned.Contains(row.RequiredFacilityIdx.Value) ||
                row.MinStoreStage.HasValue && priorStoreStage < row.MinStoreStage.Value ||
                row.RepeatMode == InspectorRepeatMode.OncePerSession && completedSession.Contains(row.Idx) ||
                completedDays.Contains((displayDay, row.Idx))) continue;
            candidates.Add(row);
        }
        selected = candidates;
        day = displayDay;
        eventIndex = 0;
        lineIndex = 0;
        phase = selected.Count == 0 ? InspectorEventPhase.Completed : InspectorEventPhase.Dialogue;
    }

    /// <summary>화면에 표시했던 날짜·이벤트·대사와 일치하는 입력을 한 번만 적용한다.</summary>
    /// <param name="expectedDay">화면의 일차.</param>
    /// <param name="eventIdx">화면의 이벤트 PK.</param>
    /// <param name="expectedLineIndex">화면의 대사 인덱스.</param>
    /// <returns>유효한 입력을 적용했으면 true.</returns>
    public bool Advance(uint expectedDay, uint eventIdx, int expectedLineIndex)
    {
        if (phase != InspectorEventPhase.Dialogue || expectedDay != day ||
            selected[eventIndex].Idx != eventIdx || lineIndex != expectedLineIndex) return false;
        if (lineIndex + 1 < selected[eventIndex].DialogueTextIdxs.Length) lineIndex++;
        else phase = InspectorEventPhase.AwaitingExit;
        return true;
    }

    /// <summary>유효한 퇴장 완료만 이력에 기록하고 다음 이벤트 또는 완료 상태로 이동한다.</summary>
    /// <param name="expectedDay">퇴장을 시작한 일차.</param>
    /// <param name="eventIdx">퇴장을 시작한 이벤트 PK.</param>
    /// <returns>이번 완료 통지를 적용했으면 true.</returns>
    public bool CompleteExit(uint expectedDay, uint eventIdx)
    {
        if (phase != InspectorEventPhase.AwaitingExit || expectedDay != day || selected[eventIndex].Idx != eventIdx) return false;
        if (selected[eventIndex].RepeatMode == InspectorRepeatMode.OncePerSession) completedSession.Add(eventIdx);
        completedDays.Add((day, eventIdx));
        eventIndex++;
        lineIndex = 0;
        phase = eventIndex == selected.Count ? InspectorEventPhase.Completed : InspectorEventPhase.Dialogue;
        return true;
    }
}
