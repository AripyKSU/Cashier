using System;
using UnityEngine;

/// <summary>
/// 영업 화면 안에서 감독관이 해야 할 일을 그 순간에 알려 주는 튜토리얼 진행자.
/// 1일차: 첫 손님 작업대(오른쪽 판매·왼쪽 제외) → 계산기 → 첫 거래 후 영업시간·속도·손님 성향 설명.
/// 1일차 첫 거래 설명이 끝날 때까지 영업 시계를 멈춰 둡니다. 3일차: 첫 작업대에서 지침 설명.
/// 화면 표시는 <see cref="TutorialCoachPresenter"/>가 하고, 이 클래스는 언제 무엇을 말할지만 정합니다.
/// </summary>
public sealed class InGameTutorialDirector
{
    private static readonly uint[] SortingLines = { 8554, 8555 };
    private static readonly uint[] CalculatorLines = { 8556 };
    private static readonly uint[] AfterFirstTradeLines = { 8557, 8558, 8559, 8560 };
    private static readonly uint[] GuidelineLines = { 8561, 8562 };

    private readonly TutorialCoachPresenter coach;
    private readonly SaleSortingPanel sortingPanel;
    private readonly RectTransform hud;
    private readonly Func<uint, string> getText;

    private bool sortingShown;
    private bool calculatorShown;
    private bool firstTradeDone;
    private bool afterTradeShown;
    private bool dayOneFinished;
    private bool guidelineShown;
    private DayProgressState lastState;

    /// <summary>튜토리얼에 필요한 화면 요소를 연결합니다.</summary>
    /// <param name="coach">안내 말풍선 Presenter입니다.</param>
    /// <param name="sortingPanel">작업대 구역과 계산기를 가진 패널입니다.</param>
    /// <param name="hud">시각·보유금 명패. 없으면 시계 강조를 생략합니다.</param>
    /// <param name="getText">TextData 키를 문자열로 바꾸는 함수입니다.</param>
    /// <exception cref="ArgumentNullException">필수 인자가 null인 경우 발생합니다.</exception>
    public InGameTutorialDirector(TutorialCoachPresenter coach, SaleSortingPanel sortingPanel, RectTransform hud,
        Func<uint, string> getText)
    {
        this.coach = coach ?? throw new ArgumentNullException(nameof(coach));
        this.sortingPanel = sortingPanel ?? throw new ArgumentNullException(nameof(sortingPanel));
        this.hud = hud;
        this.getText = getText ?? throw new ArgumentNullException(nameof(getText));
    }

    /// <summary>
    /// 1일차 설명이 끝나기 전에는 영업 시계를 멈춰 둡니다. 안내가 떠 있는 동안에도 멈춥니다.
    /// </summary>
    public bool IsHoldingTime { get; private set; }

    /// <summary>매 프레임 하루 진행 상태를 보고 필요한 안내를 띄웁니다.</summary>
    /// <param name="day">현재 하루 진행입니다. null이면 아무것도 하지 않습니다.</param>
    /// <param name="displayDay">1부터 시작하는 표시 일차입니다.</param>
    public void Tick(DayProgress day, int displayDay)
    {
        if (day == null)
        {
            IsHoldingTime = false;
            return;
        }

        DayProgressState state = day.State;
        bool trading = state == DayProgressState.Operating || state == DayProgressState.Sorting ||
            state == DayProgressState.TransactionResult;

        if (displayDay == 1 && !dayOneFinished)
        {
            tickDayOne(state);
            IsHoldingTime = trading && !dayOneFinished;
        }
        else
        {
            if (displayDay == 3 && !guidelineShown && state == DayProgressState.Sorting && sortingPanel.IsSorting && !coach.IsShowing)
            {
                guidelineShown = true;
                coach.Show(texts(GuidelineLines), new[] { sortingPanel.ExcludedZone, sortingPanel.ExcludedZone }, null);
            }

            IsHoldingTime = trading && coach.IsShowing;
        }

        lastState = state;
    }

    /// <summary>1일차 첫 손님 동안 단계별 안내를 띄웁니다.</summary>
    /// <param name="state">현재 하루 진행 상태입니다.</param>
    private void tickDayOne(DayProgressState state)
    {
        // 안내가 떠 있는 동안에도 첫 거래 완료는 놓치지 않도록 먼저 기록한다.
        if (lastState == DayProgressState.TransactionResult && state != DayProgressState.TransactionResult)
            firstTradeDone = true;
        if (coach.IsShowing) return;

        if (!sortingShown && state == DayProgressState.Sorting && sortingPanel.IsSorting)
        {
            sortingShown = true;
            coach.Show(texts(SortingLines), new[] { sortingPanel.SaleZone, sortingPanel.ExcludedZone }, null);
            return;
        }

        if (sortingShown && !calculatorShown && sortingPanel.IsCalculatorOpen)
        {
            calculatorShown = true;
            coach.Show(texts(CalculatorLines), new[] { sortingPanel.CalculatorPanel }, null);
            return;
        }

        if (firstTradeDone && !afterTradeShown)
        {
            afterTradeShown = true;
            coach.Show(texts(AfterFirstTradeLines), new[] { null, hud, null, null }, () => dayOneFinished = true);
        }
    }

    /// <summary>TextData 키 목록을 문자열로 바꿉니다.</summary>
    /// <param name="idxs">TextData 키 목록입니다.</param>
    /// <returns>같은 순서의 문자열입니다.</returns>
    private string[] texts(uint[] idxs)
    {
        var result = new string[idxs.Length];
        for (int index = 0; index < idxs.Length; index++) result[index] = getText(idxs[index]);
        return result;
    }
}
