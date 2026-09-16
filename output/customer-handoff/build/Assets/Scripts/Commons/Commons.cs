using System;
using System.Collections.Generic;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using UnityEngine;

// =========================================================================
// 1. ENUMS (열거형 데이터 - None = 0 ~ TypeName_End 규칙)
// =========================================================================

/// <summary>
/// DataTableManager에서 관리하는 데이터 테이블 타입 열거형 (우선순위 순서대로 정렬)
/// </summary>
public enum DataTableType : uint
{
    None = 0,
    
    // [1순위: 기반 공용 데이터]
    Resource = 1,         // 1순위: Addressable 에셋 리소스 데이터 (1001~)
    Text = 2,             // 2순위: 다국어/표기 텍스트 데이터 (2001~)
    
    // [2순위: 유닛 파생/개별 데이터]
    PlayerData = 4,       // 4순위: 플레이어 파생 데이터 (4001~)
    
    DataTableType_End
}


// =========================================================================
// 2. INTERFACES (인터페이스 데이터)
// =========================================================================

/// <summary>
/// CSV 데이터 테이블 로딩 표준 인터페이스
/// </summary>
public interface IDataLoad
{
    public int GetDataCount();
    public void LoadData(string csvText);
    public void Release();
}


// =========================================================================
// 3. STRUCTS (구조체 데이터)
// =========================================================================


// =========================================================================
// 4. CLASSES (CsvHelper 모델 및 DTO 데이터)
// =========================================================================

/// <summary>
/// 전역 공통 상수 및 설정 클래스
/// </summary>
public static class CommonConstants
{
    public const string AddressableLabelDatas = "Datas";
    public const string AddressableLabelAnims = "Anims";
    public const string AddressableLabelPrefabs = "Prefabs";
    public const float ParryWindowDuration = 0.15f;
}

/// <summary>
/// Addressable 에셋 참조 데이터 (ResourceData.csv 1:1 매핑, Type 1: 1001~)
/// </summary>
[Serializable]
public class ResourceData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("path")]
    public string Path { get; set; } // Addressable Key ("Player", "GaronAnimatorController" 등)
}

/// <summary>
/// 텍스트 데이터 (TextData.csv 1:1 매핑, Type 2: 2001~)
/// </summary>
[Serializable]
public class TextData
{
    [Name("idx")]
    public uint Idx { get; set; }
    [Name("kr")]
    public string Kr { get; set; }
}