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
    
    // CSV_RULES.md의 종류 ID. 숫자 순서는 로딩 의존 순서를 의미하지 않는다.
    Product = 1,          // 상품: 1001~1999
    Balance = 2,          // 밸런스: 2001~2999, CSV·로더는 아직 미구현
    Resource = 3,         // Addressable 리소스: 3001~3999
    
    // [2순위: 유닛 파생/개별 데이터]
    PlayerData = 4,       // 4순위: 플레이어 파생 데이터 (4001~)
    CustomerAppearance = 5, // 손님 외형: 5001~5999
    CustomerDisposition = 6, // 손님 성향: 6001~6999
    ProductCategory = 7,     // 상품군: 7001~7999
    Text = 8,               // 명시적 문자열 테이블: 8001~8999
    
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
/// Addressable 에셋 참조 데이터 (ResourceData.csv 1:1 매핑, Type 3: 3001~)
/// </summary>
[Serializable]
public class ResourceData
{
    /// <summary>현재 리소스 PK, 3001~3999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>명시적으로 문자열을 허용하는 Addressables 키.</summary>
    [Name("path")]
    public string Path { get; set; } // Addressable Key ("Player", "GaronAnimatorController" 등)
}

/// <summary>
/// 표시 문자열의 단일 원본 (TextData.csv, Type 8: 8001~). 다른 CSV는 nameidx로 참조한다.
/// </summary>
[Serializable]
public class TextData
{
    /// <summary>표시 문자열 PK, 8001~8999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>실제 표시 문구. 명시적으로 문자열을 허용하는 text 열.</summary>
    [Name("text")]
    public string Text { get; set; }
}
