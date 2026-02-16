namespace ExchangeRate.Core.Enums;

public enum ErrorTypes
{
    GenericError = 1,
    MissingInput = 2,
    InvalidValue = 3,
    IncorrectFormat = 4,
    OwnVatNumberProvided = 5,
    InvalidVatNumber = 6,
    DuplicateInvoiceNumber = 7,
    MissingOwnVatNumber = 8,
    VatNumberCountryMismatch = 9,
    InvalidVatRateForCountry = 10,
    InvalidVatRateForDate = 11,
    DateBelongsToPreviousPeriod = 12,
    DateBelongsToFuturePeriod = 13,
    InconsistentValue = 14,
    AdjustmentRuleApplied = 15,
    DynamicRuleExecutionError = 16,
}
