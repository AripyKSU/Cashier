using System;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

/// <summary>누적 도덕성 구간별 딸 대사 후보.</summary>
public sealed class DaughterDialogueData
{
    /// <summary>대사 구간 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>포함 하한. null은 음의 무한대.</summary>
    [Name("morality_min")] public decimal? MoralityMin { get; set; }
    /// <summary>미포함 상한. null은 양의 무한대.</summary>
    [Name("morality_max")] public decimal? MoralityMax { get; set; }
    /// <summary>동일 확률로 선택할 TextData FK.</summary>
    [Name("text_idxs"), TypeConverter(typeof(UIntArrayConverter))] public uint[] TextIdxs { get; set; }

    /// <summary>PK 대역·경계·후보를 FK 검사 전에 확인한다.</summary>
    /// <exception cref="ArgumentException">필수값·범위·후보 오류.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.DaughterDialogue || Idx % 1000 == 0 ||
            MoralityMin >= MoralityMax || TextIdxs == null || TextIdxs.Length == 0)
            throw new ArgumentException($"DaughterDialogue PK={Idx}: 필수값·범위 오류");
        var ids = new System.Collections.Generic.HashSet<uint>();
        foreach (uint textIdx in TextIdxs)
            if (textIdx == 0 || !ids.Add(textIdx))
                throw new ArgumentException($"DaughterDialogue PK={Idx}: text_idxs 0 또는 중복 불가");
    }
}
