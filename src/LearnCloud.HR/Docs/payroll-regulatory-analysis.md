# Payroll Regulatory Analysis — LearnCloud HR Module
**Highest regulatory risk in the product. Must decide: build, integrate, or export.**
**HQ Bulawayo, ZW. Date: 2026-08-03. Author: LearnCloud Product Team. Status: DRAFT FOR ACCEPTANCE BEFORE CODE**

## Executive Summary

Payroll looks simple — salary minus deductions — but is the highest regulatory risk in LearnCloud because every payslip is a legal declaration to ZIMRA, NSSA, ZIMDEF, NEC and the employee. A cent wrong, a late submission, or a misclassification creates personal liability for the school and for us as the software provider if we calculate the deduction.

**Recommendation: DO NOT build in-house payroll calculation for V1.** Build HR records, contracts, leave, appraisals, disciplinary, reporting, headcount/turnover/leave liability. For payroll, build **payroll-ready export (CSV + PDF) + integration hooks to existing payroll products (Belina, Paymaster, Pastel Payroll, etc.)** and offer it as "Export to payroll" rather than "Payroll" in the product.

If we must calculate, ring-fence it as a separate, audited, versioned, jurisdiction-specific calculation service with explicit disclaimers and require a local payroll expert sign-off per tenant. Even then, liability remains.

This document lists statutory obligations varying by jurisdiction, what must be configurable, what liability we take on, and honest build vs integrate vs export analysis.

---

## 1. Statutory Obligations That Vary by Jurisdiction

LearnCloud target market: independent schools 150-2,000 learners in Zimbabwe, but many have staff in South Africa (remote), Zambia (border schools), or diaspora. Even within Zimbabwe, rules differ by sector (NEC Education vs NEC General) and by school type.

### Zimbabwe (Primary Jurisdiction - HQ Bulawayo)

**Tax (ZIMRA):**
- PAYE (Pay As You Earn) - progressive bands that change every budget. Example 2024: 0% up to $100, 20% $100.01-$500, 25% $500.01-$1500, 30% $1500.01-$3500, 35% above $3500, plus AIDS levy 3% of PAYE. Bands and levy change annually in national budget, sometimes mid-year via Statutory Instrument. Must be configurable per effective date.
- Taxable income includes basic salary, allowances (housing, transport, cost of living), bonuses, overtime, benefits in kind (accommodation, school fees for staff children).
- Non-taxable or partially: some allowances, pension contributions up to limits, medical aid.
- Filing: PAYE must be remitted by 10th of following month, P2 forms monthly, annual QPDs.

**Social Security:**
- **NSSA (National Social Security Authority):** Pension: 4.5% employee + 4.5% employer on insurable earnings up to ceiling that changes (e.g., $700, $1000). Ceiling changes via SI, must be configurable. Workman's Compensation: rate varies by industry (education ~1-2% employer only), also changes.
- **NSSA Compliance:** Must generate NSSA P4, monthly remittance, late penalty 10% + interest.

**Skills Development:**
- **ZIMDEF (Manpower Development):** 1% of wage bill employer, must be remitted monthly, rate can change.

**Labour Law:**
- **NEC (National Employment Council) for Education:** Minimum wages per grade (e.g., unskilled, semi-skilled, skilled, teacher bands), leave entitlements (22 days annual for 5-day week, 30 days for 6-day week), sick leave 90 days full pay, maternity 98 days, 13th cheque? Some NECs require 13th cheque as bonus. Overtime rates: 1.5x normal, 2x Sunday/public holiday. Must be configurable per NEC.

**Other:**
- **Medical Aid:** 50/50 employer/employee or per contract, varies by medical aid society.
- **Union dues:** ZIMTA, etc.

**Example Zimbabwe complexity:** A teacher with basic $600, housing allowance $100, transport $50, bonus $200 in December. Taxable = 600+100+50+200 = 950 minus pension (4.5% of $700 ceiling = $31.50) = 918.50 taxable. PAYE on 918.50 using bands 2024: $100 @0% =0, $400 @20%=80, $418.50 @25%=104.625 => PAYE 184.625 + AIDS levy 3% = 5.53875 => total PAYE 190.16. NSSA employee 31.50, employer 31.50, ZIMDEF 1% of 950 =9.50, medical aid $40 employee $40 employer. Net pay etc. Each number must be exactly per SI, and bands change.

### South Africa (Secondary - if remote staff)

- PAYE progressive bands different, UIF 1% employee +1% employer up to ceiling R17,712, SDL 1% employer, medical tax credits, ETI incentives. SARS filing via EMP201 monthly, EMP501 biannual. Completely different from ZW.

### Zambia (Border schools)

- PAYE bands different, NAPSA pension 5% employee +5% employer up to ceiling K1,149, etc., NHIS health insurance.

### Summary Table of Varying Obligations

| Obligation | ZW | SA | ZM | Configurable? |
|---|---|---|---|---|
| PAYE bands | Annual budget changes, AIDS levy 3% | Different progressive, no AIDS levy | Different | Yes, per effective date, per jurisdiction |
| PAYE taxable components | Basic+allowances+bonus+benefits, some exempt | Different set | Different | Yes, taxability per allowance type |
| NSSA/NAPSA/UIF rate | 4.5%+4.5% ceiling $700-1000 changes via SI | 1%+1% ceiling R17712 | 5%+5% ceiling K1149 | Yes, rate and ceiling per effective date |
| Workman's Comp / SDL / ZIMDEF | 1-2% employer, 1% employer | 1% SDL employer | Skills levy | Yes |
| Min wage per NEC | Education NEC vs General, grades | BCEA, Sectoral Determination | Minimum Wages Order | Yes, per NEC/sector |
| Leave entitlement | 22 days 5-day week, 30 days 6-day, 90 days sick, 98 days maternity | 15 days annual, 30 days sick per 3-year cycle, 4 months maternity | 24 days annual | Yes, per leave type |
| Overtime | 1.5x normal, 2x Sunday | 1.5x | 1.5x | Yes |
| Bonus/13th cheque | Some NEC require | Not mandatory | Some require | Yes |
| Medical aid | 50/50 or per contract | Medical tax credits | NHIS | Yes |

**Conclusion:** At least 20+ parameters that vary by jurisdiction and by year, and change via Statutory Instruments with little notice.

---

## 2. What Must Therefore Be Configurable (If We Build)

If we decide to calculate deductions, the following must be configurable per tenant AND per effective date (because SI changes mid-term):

**Per jurisdiction (ZW, SA, ZM):**
- PAYE bands: array of {from, to, rate} with effective_from, effective_to
- AIDS levy / UIF / SDL rates and whether applied on PAYE or on gross
- Pension/NSSA/NAPSA rate employee and employer, ceiling amount, effective date
- Workman's Comp/SDL/ZIMDEF rate employer, effective date
- Tax credit formulas (medical aid tax credit SA)
- List of allowance types and taxability boolean: housing allowance taxable? transport taxable? school fees benefit for staff children taxable? Cost of living allowance?
- Minimum wage per NEC per grade, overtime multipliers

**Per tenant/school:**
- Which NEC applies (Education, General, maybe none for independent schools)
- Which pension fund (NSSA vs private)
- Which medical aid and contribution split
- Whether 13th cheque is mandatory and when
- Leave types entitlements per NEC and per contract type
- Currency (ZWG, USD, ZAR) - payroll may be multi-currency
- Pay frequency: monthly, fortnightly

**Per employee:**
- Tax status: resident/non-resident, disabled, over 55, etc. (affects bands in some jurisdictions)
- Pension opt-in/out
- Medical aid membership and dependents count
- Union membership
- Allowances assigned (housing, transport, acting, etc.) and amounts

**Per payroll run:**
- Gross to net formula version
- Proration for mid-month joiners/leavers
- Bonus, overtime hours, deductions (loans, garnishees)

**Configuration UI complexity:** For PAYE alone, need UI to edit bands with effective dates, with validation sum to 100%? Actually bands must be contiguous and non-overlapping, with audit log.

**Estimated configurable fields: 50+ fields, each with effective date, each requiring audit, each with validation.**

---

## 3. What Liability I Take On By Calculating Deductions

**If we calculate PAYE, NSSA, ZIMDEF and generate payslip with net pay:**

1. **Tax liability:** If we under-calculate PAYE, school under-remits to ZIMRA, school gets penalty 10% + interest, but may claim we provided wrong calculation. ZIMRA does not accept "software error" as excuse. School may seek damages from us. Even with disclaimer "check with accountant", if our UI shows net pay $800 and we calculated PAYE $100 but correct PAYE $150, employee was overpaid $50, school suffers loss recovering overpayment, may sue.

2. **Labour law liability:** If we calculate leave entitlement wrong (e.g., 22 days vs 30 days for 6-day week), employee may claim underpayment of leave upon termination, school liable, may claim software error.

3. **NSSA liability:** Under-remittance of NSSA affects employee pension. Employee could claim loss.

4. **Personal liability of directors:** In ZW, directors can be personally liable for PAYE not remitted if company fails to remit. If software miscalc leads to failure to remit correct amount, director risk.

5. **Professional indemnity:** Payroll software providers in ZW/SA typically have professional indemnity insurance and employ chartered accountants and labour lawyers to keep rules updated. As a small team in Bulawayo, we do not have that.

6. **Versioning and audit:** Must keep every payroll run calculation formula versioned, with exact bands used, so that 3 years later when ZIMRA audits, we can reproduce exact calculation as at that date. Requires immutable audit log of bands and calculation steps.

7. **Jurisdiction creep:** Once we support ZW, schools will ask for SA, then ZM. Each new jurisdiction multiplies complexity and liability.

**Disclaimers do not fully protect:** Even with "This is not tax advice, consult accountant" in terms, if our software is marketed as calculating PAYE and generates payslip with PAYE amount, courts may hold us as providing tax calculation service, not just tool. In ZW, there is precedent of software providers being held liable for incorrect statutory calculations if they held themselves out as compliant.

**Quantified risk:** For 100 schools * 50 employees average = 5000 employees * 12 months = 60,000 payslips per year. If 1% error rate (600 payslips) with average error $20, potential exposure $12,000 direct, plus penalties, plus reputational risk, plus legal costs. For one operator with no dedicated ops team, this is highest regulatory risk in product, as you said.

---

## 4. Honest Recommendation: Build HR, Export Payroll-Ready File, Integrate, Don't Calculate Deductions In-House (V1)

### Option A: Build Full Payroll Calculation (Not Recommended for V1)

**Pros:**
- Full control, no third party dependency
- Can charge higher price for payroll module
- Single system for school

**Cons:**
- Must maintain 50+ configurable parameters per jurisdiction with effective dates
- Must monitor SI changes in Government Gazette weekly (ZW publishes SI frequently)
- Need chartered accountant and labour lawyer retainer to validate changes within days
- Need professional indemnity insurance (costly)
- Liability for under/over calculation, penalties, employee claims
- Must version every calculation and keep audit 7 years
- Must support multi-currency, multi-NEC, multi-jurisdiction
- Build time: 3-6 months for ZW alone, 6-12 months for ZW+SA+ZM, plus ongoing maintenance 20% of dev time forever
- As one operator, highest regulatory risk, as you said

**Liability:** High. Even with disclaimers, risk of being held liable for incorrect statutory deductions.

**Recommendation:** No, not for V1, not with one operator.

### Option B: Integrate with Existing Payroll Product (Recommended for V2/V3, with caution)

**Examples:** Belina Payroll (ZW popular, local), Paymaster, Pastel Payroll (Sage), VIP Payroll (SA), QuickBooks Payroll, etc.

**How integration would work:**
- LearnCloud HR holds staff records, contracts, leave, appraisals (we build this - low risk)
- LearnCloud exports payroll input file: employee code, basic salary, allowances, overtime hours, leave days taken, deductions (loans), but NOT calculated PAYE/NSSA - just gross inputs
- Existing payroll product calculates PAYE, NSSA, ZIMDEF, etc., using its own updated, audited, insured calculation engine
- Existing payroll product then exports back payslip PDFs and net pay, or LearnCloud imports net pay for reporting
- Integration via CSV, API, or SFTP

**Pros:**
- Liability stays with established payroll vendor who has accountants, lawyers, insurance, and keeps rules updated
- We focus on HR records, which is low risk and high value
- Schools that already use Belina/Pastel keep it, we don't force migration
- Faster to build: export file is 1 week, API integration 2-4 weeks per vendor

**Cons:**
- Dependency on third party vendor uptime, pricing, API changes
- School must pay for two products (LearnCloud HR + payroll product) - may be okay if we are cheaper than full payroll
- Integration complexity: mapping employee codes, handling mid-month changes
- Still need to handle leave and allowances correctly as inputs to payroll product
- Need to support multiple payroll products, not just one, because schools use different products

**Liability:** Low to medium. We provide gross inputs, not statutory deductions. If our gross inputs (basic salary, allowances, overtime hours) are wrong, liability limited to HR records, not tax. Still need to ensure leave balance calculation correct (leave liability is also regulatory risk but lower than PAYE).

**Recommendation:** Yes, for V2. Start with one integration - Belina (most common in ZW independent schools) via CSV. Build payroll input export first (Option C), then add API integration to Belina as paid add-on.

### Option C: Export Payroll-Ready File (Recommended for V1)

**Build HR module fully, plus export payroll-ready file (CSV + PDF) that school's accountant or existing payroll clerk can import into their payroll product or use to manually capture into Pastel/Belina.**

**What export contains (payroll-ready, but NOT calculated deductions):**
- Employee code, national ID, full name, department, designation, employment type, hire date, pay frequency
- Basic salary, allowances breakdown (housing, transport, COLA, acting), each with taxability flag (for accountant to decide, not us calculating)
- Overtime hours, overtime rate, leave days taken (annual, sick, maternity), leave balance remaining, unpaid leave days
- Deductions that are NOT statutory: loans, advances, union dues, savings, etc. (entered by bursar)
- Bank details: bank name, account number, branch
- Gross total (basic + allowances + overtime) - we calculate gross, which is low risk (addition, not statutory)
- NO PAYE, NO NSSA, NO ZIMDEF, NO medical aid tax credit calculation - those columns left blank or marked "To be calculated by payroll product/accountant"

**Plus:**
- PDF summary: total gross, total allowances, total overtime, total deductions (non-statutory), headcount, and blank columns for PAYE/NSSA to be filled by accountant
- CSV format compatible with Belina import template and Pastel Payroll import template (provide two templates)
- Audit log of export: who exported, when, which employees, period

**Pros:**
- Zero statutory deduction liability - we do not calculate PAYE, NSSA, etc.
- Fast to build: HR records 4-6 weeks, export 1 week
- Schools can use existing payroll product or accountant - no forced migration
- Still high value: school has single source of truth for staff records, contracts, qualifications, documents, leave, appraisals, disciplinary, headcount/turnover/leave liability reports, and export saves bursar hours of retyping
- Can charge for HR module without taking on payroll risk
- Leaves door open to integrate later (Option B) or build calculation later with proper insurance and accountant retainer

**Cons:**
- Not full payroll - school still needs separate payroll product or accountant to calculate deductions
- Some schools may want all-in-one and may choose competitor who offers payroll calculation (but those competitors take on risk we are avoiding)

**Liability:** Low. Gross pay calculation is addition, low risk. Leave balance calculation has some risk (leave liability), but lower than tax, and can be made configurable per NEC with effective dates, and with disclaimer to verify with labour lawyer.

**Recommendation for V1: Yes, build HR + payroll-ready export, not payroll calculation.**

### Hybrid Recommendation for Roadmap

**V1 (Now - 8 weeks):**
- Build HR module: staff records, contracts with expiry reminders, qualifications and documents, leave types with entitlements per NEC, leave request and approval workflow with balance calculation and leave calendar, appraisal cycles with configurable criteria, disciplinary records with restricted access, staff reporting headcount, turnover, leave liability
- Build payroll-ready export: CSV + PDF with gross inputs, no PAYE/NSSA/ZIMDEF calculation, two templates (Belina, Pastel), audit log
- Terms: "Payroll export is payroll-ready file for import into your payroll product or for your accountant. LearnCloud does not calculate statutory deductions. Please verify with your accountant."

**V2 (3-6 months after V1, if demand):**
- Integration with one payroll product: Belina Payroll via CSV auto-import or API. School still uses Belina to calculate deductions, but we push gross inputs automatically via SFTP/API, and pull back net pay and payslip PDFs for reporting. Liability still with Belina.

**V3 (12+ months, only if we have accountant retainer + insurance + dedicated payroll team):**
- Consider building in-house payroll calculation for ZW only, with jurisdiction flag, effective-dated PAYE bands, NSSA ceiling, etc., with explicit disclaimers, versioned calculations, audit, and require school to have accountant sign-off on first payroll run per tenant. Still high risk, but with insurance and team, may be viable.

**Final honest recommendation:** For one operator with no dedicated ops team, prefer simple and recoverable over clever. Payroll calculation is clever and high risk. Export is simple and recoverable. So export for V1.

---

## 5. What Must Be Configurable If We Ever Build Payroll Calculation

If after acceptance of this analysis you still want to build payroll calculation despite recommendation, the following must be configurable per tenant and per effective date, with audit:

- Jurisdiction: ZW, SA, ZM
- NEC: Education, General, etc.
- PAYE bands: array {from, to, rate}, effective_from, effective_to
- AIDS levy rate, UIF rate, SDL rate, whether applied on PAYE or gross
- Pension rate employee/employer, ceiling, effective date
- Workman's comp rate employer
- Taxable allowance types list
- Minimum wage per grade
- Overtime multipliers
- 13th cheque mandatory boolean
- Medical aid contribution split
- Currency
- Pay frequency
- Employee tax status, pension opt-in, medical aid dependents, union membership
- Proration rules for mid-month joiners/leavers
- Bonus, overtime, deductions

All with UI to edit, validation, audit log old/new, effective date, and test suite that reproduces ZIMRA example calculations to cent.

---

## 6. Acceptance Required Before Code

I have built HR module entities, DTOs, services, controllers, frontend (in next files). For payroll, I have NOT built calculation code, only this analysis. Please accept:

- [ ] Accept recommendation: Build HR + payroll-ready export for V1, not payroll calculation
- [ ] Or: Accept risk and instruct to build payroll calculation for ZW only with disclaimers, accountant retainer, insurance
- [ ] Or: Instruct to integrate with specific payroll product (Belina, Pastel, etc.) for V2

Only after acceptance will I build payroll code (export or integration or calculation).

