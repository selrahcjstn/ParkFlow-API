# Automated registration and OCR verification

The API processes verification through its existing .NET layers. Interfaces and matching rules live in Application, durable state and atomic source changes in Persistence, and PDF/OCR/native process execution in Infrastructure. A single hosted worker per API instance polls PostgreSQL; database leases make multiple API instances safe. No separate OCR service is required.

## Approval behavior

Account, COR, vehicle and schedule decisions are independent. Account, COR and schedule records expose Pending, Processing, PreVerified, Approved, NeedsReview and Rejected as strings. Vehicles use the existing manual Pending/Verified/Rejected status and reason. Existing COR/vehicle clients retain Verified = 2 and Rejected = 3; COR Processing = 4, PreVerified = 5 and NeedsReview = 6 are appended. A nullable `CorSubmission.ScheduleVerificationStatus` preserves legacy combined approvals until a schedule is edited.

Account, COR and schedule automatic approval toggles default to false. University academic year and semester start empty and must be configured by the Super Admin. The settings are persisted in PostgreSQL, use an optimistic version, and record before/after values and actor identifiers in the audit history. Vehicle automatic approval is permanently disabled and hidden from settings. `VehicleAutomaticApproval` and `MaxScheduleHoursPerDay` remain in the existing schema for compatibility; the latter no longer limits schedules. The older in-memory `AutoApproveVerification` field has no authority over the verification worker.

The worker verifies email status, ID format, duplicate institutional IDs, COR fields and term rules, and valid non-overlapping class intervals. There is no university maximum for daily schedule hours. OCR matching establishes information consistency; it does not establish university enrollment or document authenticity. Manual vehicle approval checks plate format, duplicates, brand and required uploaded OR/CR/plate-photo documents before an admin decides ownership.

`IInstitutionalVerificationProvider` is the integration point for authorized institutional identity or document authenticity records. The shipped implementation reports unavailable. A real provider must verify the exact subject fingerprint and return a nonempty evidence reference. Account and COR submissions therefore require manual review even when their automatic toggles are enabled. Schedule approval can run automatically when its rules pass and the COR is approved. Faculty/staff employee identity documents continue to require review and do not create personal schedule approval requests.

Account, COR and schedule manual review requires an admin, a review reason and a current record version. It can override a completed decision, but mandatory email, ID, duplicate and schedule checks still apply. COR and vehicle approvals do not activate accounts. COR replacement does not reset account approval or create a new account request for an already active account. Account activation has its own review action. Parking entry and reservation eligibility also check independent schedule approval.

OR/CR and plate photos use manual Vehicle Approvals through `PATCH`/`POST /api/vehicles/{id}/validate`. The compatible vehicle subject-review route delegates to the same manual operation. Vehicle uploads/edits/reviews create no new verification records or verification audits and invoke no OCR/trusted-provider job. Notifications remain available. Replaced documents return the vehicle to Pending; source locks and fingerprint checks protect concurrent edits. Old vehicle job records are excluded from processing, retired when the vehicle is edited or reviewed, and preserved in storage with their existing history. Vehicle automatic approval cannot be enabled through API settings or an old stored toggle.

Administrative verification policies require the signed `parkflow_authentication_method=pwd` claim, issued only after the existing password login validates credentials. Admins must sign in again after deployment. Microsoft callback and registration tokens cannot authorize these actions. The existing Microsoft callback still needs separate server-side OAuth token validation before it can establish a trusted institutional identity.

The pre-registration OTP flow returns an additional `emailVerificationToken` beside the existing boolean result. This is an email-bound, signed receipt valid for ten minutes, with a separate audience from API access tokens. Mobile sends it with registration so email ownership survives account creation. It does not approve the account. Existing clients can still register without a receipt and must verify email before account approval; authenticated owners can request a code through `/api/auth/email-verification/send`. Reset-password OTPs do not issue registration receipts.

## Documents and processing

COR uploads accept PDF, JPG/JPEG and PNG up to 5 MB. Extension/signature checks, PDF structure checks, page limits and image dimension limits run before storage. Password-protected or damaged PDFs, active actions, embedded files and XFA are rejected. The worker revalidates downloaded content and only fetches COR paths in the configured Cloudinary account or the existing local upload folder.

PdfPig extracts text from digital PDFs. Pages containing scanned information, including a digital heading over scanned fields, are rendered through Poppler's `pdftoppm` and read by Tesseract. Extracted fields include name, student number, course, academic year, semester, and optional section/year level. Unsupported layouts, missing fields, mismatches and unreadable content go to NeedsReview. Employee identity documents go to institutional review.

Raw extracted text and native stderr are never stored in verification records or logged. Recognized fields are persisted as JSON; identity fields are returned only from the admin records endpoint, while owners can fetch their derived schedule preview. Public SignalR broadcasts contain record identifiers and module types; user-specific notifications contain their own status and reason. Decision notifications use the existing persisted notification and SignalR services; approvals/rejections also retain email notification behavior.

### COR-based Student registration

Student accounts follow the existing signup/login and email verification, then upload a COR before profile details, vehicle registration and remaining documents. Personnel retains its existing profile, employee details, vehicle and employee-ID flow. Digital Student PDFs are extracted during upload; scanned PDFs and images use the existing background worker. All processing is local; successful text extraction never establishes authenticity or activates the account.

The Student review auto-fills full name, student number, College, course/program, year, section, academic year and semester when readable. Surname-first names are split only when a comma separates surname and given names. Printed middle initials are retained without expanding them; ambiguous name parts and unmapped course options remain blank for correction. Mobile maps recognized courses and year levels to its existing dropdown values. Raw extraction remains available alongside confirmed corrections in the existing COR verification JSON. College has no existing Student column and is retained in that JSON rather than adding a new schema field.

The parking preview groups raw class sessions by day, takes the earliest start and latest end, and includes gaps between classes. It displays exactly seven rows, Monday through Sunday, in AM/PM format with 24-hour values stored on the server. No Classes is distinct from Needs admin review. Student schedule rows are completely read-only. Missing or unrecognized rows require admin correction after checking the COR, or a clearer upload; a rejected document requires a replacement. There is no daily schedule-hour cap. Existing day/time validation and active-parking restrictions apply. Days marked No Classes have no ParkingSchedule row because the existing entity requires non-null start/end values; no midnight or invented interval is stored. Every active day stores exactly one interval.

Authenticated owners use GET /api/onboarding/student/cor-preview and POST /api/onboarding/student/cor-confirm. Confirmation accepts recordId, version, information (reviewed names, phone, student number, College, course, year, section, academic year, semester); daily schedules are derived only from stored extraction or an admin-reviewed correction. It reuses the existing profile/student validators, requires verified email, rejects Personnel/operator/completed-registration changes, checks ownership/current source/version and duplicate IDs, and saves UserProfile, Student, COR term and daily ParkingSchedules in one transaction. The account remains pending and onboarding advances only to Vehicle. Repeated identical confirmations are idempotent; simultaneous submissions create one profile and one set of daily windows. Existing admin permissions and approval policies remain authoritative.

GET /api/verification/cor-schedule and POST /api/verification/cor-schedule/confirm remain available for schedule review. Confirmation accepts only recordId/version and derives the daily intervals on the server. Client schedule corrections and older manual schedule API writes are forbidden. Owners can view their own extracted identity information, while administrative record/history routes retain their existing role restrictions.

The remaining document step uploads OR/CR and the plate photo using the stored COR URL and reviewed academic year. Confirmed information/week are restored from the server after an interrupted registration; edits are retained while navigating in the current session. A changed profile creates a new verification source revision but reuses the same document's stored extraction without another download/OCR pass. Replacing the document requires a fresh scan. Approval is re-evaluated under existing policy using the current source, independently of extraction caching.

For newly combined daily schedules, entry/exit calculations use the full earliest-to-latest daily attendance window, including breaks. Existing saved schedules retain their prior entry/exit behavior until edited. Schedules previously flagged solely by the removed daily-hour limit are reprocessed without OCR. OR/CR and plate-photo decisions remain manual without new vehicle verification audits. These changes add no dependency or migration.

### COR extraction performance

For a Student PDF with a usable digital text layer, the upload validates and extracts with one PdfPig document instance. Parsed fields commit atomically with the COR source and existing verification record. The request acquires the same database lease used by the worker, applies existing consistency/approval checks, and returns `data.corPreview` containing student information and all seven days. Mobile consumes a completed preview immediately without another fetch. If a worker wins the lease or inline processing is deferred, the existing status endpoint remains the fallback. Images and scanned PDFs never run native OCR on the request thread; Personnel uses its existing background document flow.

Inline processing has a ten-second cancellation budget and a thirty-second recovery lease. A deferred inline attempt returns the source to the durable queue without consuming a background OCR attempt. Request cancellation reaches MediatR, file reads, validation, database writes and inline checks. Existing native job/process timeouts, memory/output limits and single-worker concurrency remain active. The existing Cloudinary upload interface has no cancellation-token parameter; an in-flight SDK upload finishes before the next cancellation check.

The reader avoids a second PDF open, skips only pages with no text, images or drawn paths, and uses PDF image placement to avoid forcing OCR because of a small logo. A large image with missing identity fields or an unreadable class table still requires OCR. Scanned rendering remains at the existing 2,500-pixel scale to preserve readability. Tesseract runs in an isolated, bounded process per selected page; no resident engine, image preprocessing dependency or new service was added.

Field labels are scanned once with reusable compiled, timeout-bounded expressions. Daily aggregation uses one pass over class sessions; the preview reuses that result for both items and the seven-day week. Preview queries fetch one current COR record rather than unrelated module records. Worker reprocessing first uses the current record's stored extraction before searching history. Mobile polling for background OCR is one second, with one request in flight per polling loop; the server's configurable idle worker poll remains five seconds by default.

Structured `Stopwatch` logs measure file reads, PDF text extraction, native rendering/OCR, field parsing, storage upload/fetch, source/job database save, preview mapping and the upload API. They contain stage names, durations, outcomes and counts, with no names, student numbers, filenames, storage URLs or document contents. File-read detail uses Debug level; stage timings use Information level.

Each revision has its own record; audits survive resource deletion. Source edits and durable jobs commit together for account identity/profile/role data, CORs, vehicles and schedules. Identity changes invalidate the account decision in the same transaction; changing a profile photo preserves approval. Advisory locks serialize source edits with decisions. Current revisions have a partial unique index. Claims use expiring leases, and completion checks the lease, fingerprint, current policy and review version. Manual review invalidates an in-flight worker lease. Transient processing errors retry with backoff, then go to NeedsReview after the configured attempt limit. A resubmission supersedes the previous revision while retaining its result/audit history.

Cloudinary and local upload locations are preserved. The previous COR blob is deleted according to the existing replacement policy after the replacement and its job commit successfully. Historical verification results remain available; historical blobs are not retained by this policy.

## PostgreSQL migration

Apply `20261013040826_AddAutomatedVerification` before starting the new API. It adds VerificationPolicies, VerificationRecords and VerificationAudits, the nullable schedule status, and a unique expression index for plate numbers normalized by case, spaces and hyphens. Existing accounts, CORs, schedules and vehicles are preserved. The expression index is managed with SQL in this migration because EF's property-index model does not express it.

If legacy normalized plate duplicates exist, PostgreSQL rejects index creation and rolls the migration back. Inspect and resolve those conflicts before deployment; no records are automatically deleted or rewritten. An example preflight query is:

```sql
SELECT upper(replace(replace("PlateNumber", '-', ''), ' ', '')) AS plate, count(*)
FROM "Vehicles"
GROUP BY 1 HAVING count(*) > 1;
```

Use the existing migration workflow, or set `PARKFLOW_MIGRATION_CONNECTION` for the design-time factory and run:

```sh
dotnet ef database update --project ParkFlow.Persistence --startup-project ParkFlow.API
```

Deploy the API before the updated Admin and Mobile clients. The new Admin screens use `/verification/policies`, `/verification/records`, `/verification/audit` and versioned review endpoints. Regular Admin can view approval policies; only Super Admin can save them. The existing approval inspectors also show results and history. Mobile keeps its onboarding steps, shows status on Home and document setup, refreshes through SignalR/polling, and provides resubmission/email verification actions.

## Linux deployment

The Dockerfile installs Tesseract, English language data and Poppler into the final .NET image. On a Debian/Ubuntu host outside Docker, install:

```sh
apt-get update
apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-eng poppler-utils
```

Keep the native tools patched. The application identity needs permission to execute them and create private temporary directories. Temporary files are removed after processing. Allow at least 1 GB of container memory for the API plus its single worker, and set an actual host/container memory cap. Native process resident memory is checked during processing; PdfPig runs in the .NET process and relies on the host/container cap in addition to byte/page/structure limits.

The following optional configuration uses the defaults; no development-machine path is embedded in application code:

```json
{
  "Ocr": {
    "TesseractPath": "tesseract",
    "PdfRendererPath": "pdftoppm",
    "Language": "eng",
    "MaxFileBytes": 5242880,
    "MaxPages": 5,
    "MaxImagePixels": 20000000,
    "MaxTextCharacters": 100000,
    "MaxProcessMemoryMb": 256,
    "JobTimeoutSeconds": 120,
    "ProcessTimeoutSeconds": 30,
    "PollSeconds": 5,
    "MaxAttempts": 3
  }
}
```

`Ocr:TessdataDirectory` is optional when distribution language data is discovered by Tesseract. Environment variables use the normal .NET `Ocr__...` form. Invalid or excessive resource limits fail startup. Each process is launched with an argument list and no shell; one OpenMP thread limits native CPU use.

## Testing

The existing .NET test project includes real digital PDF extraction, native JPG/PNG/scanned/mixed PDF OCR, smart student auto-fill, daily aggregation, missing/mismatched fields, legacy manual corrections, confirmation ownership/idempotence/concurrency, extraction reuse, safety checks, approval gates, role authorization, OTP receipts, retries, resubmission, concurrent claims/reviews and normalized plate conflicts. The GitHub workflow installs native tools and supplies a disposable PostgreSQL service.

Enable the native and database tests locally with:

```sh
export PARKFLOW_NATIVE_OCR_TESTS=1
export PARKFLOW_VERIFICATION_TEST_CONNECTION='Host=localhost;Database=parkflow_verification_test;Username=postgres;Password=YOUR_TEST_PASSWORD'
dotnet test Test/Test.csproj -c Release
```

The test database name must end in `_verification_test`. Tests create and remove unique schemas, and require a disposable database. Optional `PARKFLOW_TEST_TESSERACT` and `PARKFLOW_TEST_PDFTOPPM` override test executable paths. When these opt-in environment variables are absent, native/database tests explicitly skip; unit tests still run.

`PARKFLOW_COR_SAMPLE` optionally points to the locally supplied reference digital COR (eleven raw class sessions, four combined daily windows and a seven-day preview). Its regression check reads the original file without copying personal documents into the repository. It skips when that environment variable is absent.

Opt-in performance probes use `PARKFLOW_COR_PERFORMANCE=1` and the same sample/native/database settings. Filter on `FullyQualifiedName~CorPerformanceTests|FullyQualifiedName~MeasureDigitalServerPipeline`. Optional `PARKFLOW_COR_PERFORMANCE_OUTPUT`, `PARKFLOW_COR_PERFORMANCE_NATIVE_OUTPUT` and `PARKFLOW_COR_PERFORMANCE_API_OUTPUT` save timing-only JSON. Digital tests warm the reader and measure twenty runs; parser/aggregation tests measure 2,000 iterations; native OCR uses three timed runs per format; the PostgreSQL pipeline uses six timed runs after warm-up. No test asserts a speed threshold. Pipeline timing uses in-memory storage and disabled notification transport, with real PostgreSQL; it excludes network, HTTP serialization and queue waits, so it is not a production end-to-end guarantee.

## Operational limits

- No authorized institutional record source was supplied. Account and COR automatic approval cannot activate until a trusted provider is implemented and succeeds. Vehicle documents always use manual review.
- COR label extraction is conservative. Actual university document layouts and course naming variants need representative approved samples; unsupported forms remain reviewable.
- Existing approvals are retained. Verification records and audits begin when the new workflow processes or reviews a source; previous reviewer/time information is not invented.
- Policy updates apply to jobs when processed. Completed manual decisions are retained; enabling a toggle does not retroactively approve historical submissions.
- Admin records responses currently return at most 1,000 records and audit responses at most 200 entries. Versioned review uses direct record lookup; expand pagination when required by deployment volume.
- SignalR/email failures cannot undo a committed decision. Mobile polling and persisted records remain the status source.
- Native Android/iOS builds, physical-device UI testing and a Linux Docker build require their respective deployment tools; this implementation is validated with source builds, client tests, native Windows OCR and disposable PostgreSQL tests.
- The refreshed NuGet audit reported high-severity advisories for existing Microsoft.OpenApi 2.4.1 and System.Security.Cryptography.Xml 10.0.7. Review and update those dependencies as part of deployment preparation.

Dependency references: [PdfPig](https://github.com/UglyToad/PdfPig), [Tesseract command-line documentation](https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html), [Poppler](https://poppler.freedesktop.org/).

## Read-only schedule review and correction

Students request review through POST /api/verification/cor-schedule/request-review with recordId, version and reason. A current owned COR extraction is required. The request is audited, preserves COR authenticity/account/vehicle decisions, and holds schedule verification in NeedsReview. Personal fields remain editable, while confirmation waits for a valid schedule and review resolution.

Authorized AdminOrSuperAdmin sessions correct a current COR through POST /api/verification/records/{id}/cor-schedule with version, week (seven unique dayOfWeek/hasClasses/startTime/endTime rows), reason and corReviewed=true. The admin website exposes this in the existing verification panel under the COR record. It requires checking the uploaded document and attesting review; the audit records the actor, reason and before/after daily values. Corrections use the existing transactional store and require a current version/document plus no active parking session. They reset schedule review without approving account, COR or vehicle. Schedule approval stays blocked while a Student review request is unresolved.

COR JSON retains original extraction and a separate AdminWeek correction. Legacy ConfirmedWeek is retained for compatibility but is never trusted as Student authority. No new columns or migration are required. The read-only update was compiled/type-checked; existing tests for the previous editable-week behavior have not been updated or run in this change.

## Current Student workflow: vehicle-only manual review

This supersedes Student account/COR/schedule admin review described earlier. COR extraction remains durable and schedules remain read-only. Confirming validated personal information and daily schedules completes the Student account/COR checks without admin approval. Email verification, duplicate IDs, readable schedules, suspension and existing parking-time validation remain enforced. Only OR/CR and vehicle photos retain manual vehicle review.

No new Student account/COR/schedule audits or admin approval notifications are generated. Historical data remains stored. Student account/COR/schedule records are hidden from admin review lists and their manual review routes reject changes. Unreadable schedules require a clearer COR, rather than an admin correction request. Personnel account/employee-ID review is retained. No new schema migration is needed. Existing pending Students can confirm their COR in Schedule Setup to use the new flow; no historical rows were bulk-modified.

## Current COR upload/fetch flow (supersedes inline extraction)

Student COR upload validates and stores the file without extracting information. The durable record waits with OcrOutcome AwaitingInformationFetch; claim queries exclude that state. POST /api/verification/cor-information/fetch with recordId/version checks ownership/current source and releases the job to the existing worker. It returns promptly with a preview containing fetchRequested, without doing OCR in the HTTP request. Duplicate requests reuse the same job/result. The Mobile app polls short preview GETs only after fetch is requested and restores waiting/processing states after restart. Personnel employee-ID processing is unchanged. No schema migration is required.
