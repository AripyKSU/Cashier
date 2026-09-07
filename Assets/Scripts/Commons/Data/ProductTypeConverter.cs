using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

/// <summary>CSV에서 문자열 enum 이름을 허용하지 않는 숫자 분류 변환기.</summary>
public sealed class ProductTypeConverter : DefaultTypeConverter
{
    /// <summary>정의된 숫자 상품 분류만 변환한다.</summary>
    /// <param name="text">숫자 코드.</param>
    /// <param name="row">CSV 행.</param>
    /// <param name="memberMapData">컬럼 매핑.</param>
    /// <returns>유효한 상품 분류.</returns>
    /// <exception cref="FormatException">숫자가 아니거나 정의되지 않은 코드.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ||
            value == 0 || !Enum.IsDefined(typeof(ProductType), value))
            throw new FormatException($"product_type={text}: 정의된 숫자 분류 필요");
        return (ProductType)value;
    }
}
