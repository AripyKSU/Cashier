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
    EconomyBalance = 2,   // 경제 기본 설정
    MaintenanceBalance = 3, // 회차별 상납금
    Resource = 4,         // Addressable 리소스
    
    // [2순위: 유닛 파생/개별 데이터]
    CustomerAppearance = 5, // 손님 외형: 5001~5999
    CustomerDisposition = 6, // 손님 성향: 6001~6999
    ProductCategory = 7,     // 상품군: 7001~7999
    Text = 8,               // 명시적 문자열 테이블: 8001~8999
    
    PriceEvent = 9,
    PriceEventSchedule = 10,
    ReputationBalance = 11,
    Facility = 12,
    DailyGuideline = 13,
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
