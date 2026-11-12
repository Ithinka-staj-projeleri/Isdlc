import { PROJECT_STATUS_LABELS } from '../../shared/models/project-status';
import { DOCUMENT_STATUS_LABELS } from '../documents/models/document';
import { MEETING_TYPE_LABELS } from '../meetings/models/meeting';
import { OPEN_ISSUE_STATUS_LABELS, OPEN_ISSUE_TYPE_LABELS } from '../open-issues/models/open-issue';
import {
    PROJECT_PRIORITY_LABELS, REQUEST_CHANNEL_LABELS, REQUEST_SOURCE_LABELS, REQUEST_TYPE_LABELS,
} from '../projects/project-labels';
import {
    REQUIREMENT_PRIORITY_LABELS, REQUIREMENT_STATUS_LABELS, REQUIREMENT_TYPE_LABELS,
} from '../requirements/models/requirement';
import { RISK_CATEGORY_LABELS, RISK_STATUS_LABELS } from '../risks/models/risk';
import {
    IMPROVEMENT_SOURCE_LABELS, PILOT_RESULT_LABELS,
} from '../process-improvements/models/process-improvement';
import { AuditAction, AuditEntityType } from './models/audit-log';

// Turkish text for the audit log. The API sends C# names (entity, field, enum value); the screen shows these.
// When a module adds a field, its label goes here; an unknown field is shown with its C# name.

export const AUDIT_ENTITY_LABELS: Record<AuditEntityType, string> = {
    Project: 'Proje',
    OpenIssue: 'Açık konu',
    Meeting: 'Toplantı tutanağı',
    Requirement: 'Gereksinim',
    ProjectDocument: 'Doküman',
    Risk: 'Risk',
    ProjectAssessment: 'Seviye & Kabul',
};

/**
 * Records that appear only in some projects' history (a process improvement proposal's own fields), so they are named
 * here but not offered as a filter on every project.
 */
const OTHER_ENTITY_LABELS: Record<string, string> = {
    ProcessImprovement: 'Süreç iyileştirme önerisi',
};

const OTHER_FIELD_LABELS: Record<string, Record<string, string>> = {
    ProcessImprovement: {
        Source: 'Kaynak',
        AffectedProcess: 'Etkilediği süreç',
        ProposedSolution: 'Çözüm fikri',
        SuggestedBy: 'Öneriyi getiren',
        Impact: 'Etki',
        Frequency: 'Uygulama sıklığı',
        Ease: 'Uygulama kolaylığı',
        CurrentState: 'Mevcut durum',
        RootCause: 'Kök neden',
        Solution: 'Çözüm',
        PilotPlan: 'Pilot planı',
        PilotTarget: 'Pilot hedefi',
        CostBenefit: 'Fayda / maliyet',
        PilotActual: 'Gerçekleşen',
        PilotFeedback: 'Pilot geri bildirimi',
        PilotResult: 'Pilot sonucu',
        PilotDecision: 'Pilot kararı',
    },
};

const OTHER_VALUE_LABELS: Record<string, Record<string, Record<string, string>>> = {
    ProcessImprovement: { Source: IMPROVEMENT_SOURCE_LABELS, PilotResult: PILOT_RESULT_LABELS },
};

export const AUDIT_ACTION_LABELS: Record<AuditAction, string> = {
    Created: 'oluşturuldu',
    Updated: 'güncellendi',
    Deleted: 'silindi',
};

const FIELD_LABELS: Record<AuditEntityType, Record<string, string>> = {
    Project: {
        Code: 'Proje No',
        Name: 'Proje adı',
        Description: 'Açıklama',
        Source: 'Talep kaynağı',
        RequestChannel: 'Talep kanalı',
        RequestType: 'Talep türü',
        CustomerName: 'Talep sahibi (firma / birim)',
        RequesterName: 'Talebi ileten kişi',
        RequesterContact: 'İletişim',
        Notes: 'Notlar',
        RequiresOffer: 'Teklif hazırlanacak',
        RequiresNda: 'Gizlilik sözleşmesi (NDA)',
        BusinessAnalystId: 'İş analisti',
        ProjectManagerId: 'Proje yöneticisi',
        Level: 'Seviye',
        Priority: 'Öncelik',
        Status: 'Durum',
        CurrentStepId: 'Mevcut adım',
        RequestDate: 'Talep tarihi',
        // Records written before the field was renamed (Faz 4, D1).
        StartDate: 'Talep tarihi',
        DueDate: 'Hedef bitiş tarihi',
    },
    OpenIssue: {
        Code: 'Kod',
        Type: 'Kayıt türü',
        Title: 'Konu',
        Description: 'Açıklama',
        OwnerUserId: 'Sorumlu',
        OwnerName: 'Sorumlu',
        DueDate: 'Termin',
        Status: 'Durum',
        ClosedAt: 'Kapanma zamanı',
        SourceMeetingId: 'Tutanak',
    },
    Meeting: {
        Code: 'Kod',
        Type: 'Toplantı türü',
        Date: 'Tarih',
        Participants: 'Katılımcılar',
        Agenda: 'Gündem',
        RecordedById: 'Tutanağı yazan',
        Summary: 'Toplantı özeti',
        CompletedStages: 'Tamamlanan aşamalar',
        OngoingStages: 'Devam eden aşamalar',
        NextSteps: 'Sonraki adımlar',
        ReturnDate: 'Dönüş tarihi',
        PlannedActivities: 'Planlanan aktiviteler',
        CompletedActivities: 'Yapılan aktiviteler',
    },
    Requirement: {
        Code: 'Kod',
        Type: 'Gereksinim türü',
        Title: 'Başlık',
        Description: 'Açıklama',
        AcceptanceCriteria: 'Kabul ölçütleri',
        Priority: 'Öncelik',
        Status: 'Durum',
    },
    ProjectDocument: {
        DocumentTypeId: 'Doküman',
        Status: 'Durum',
        Note: 'Not',
        Link: 'Bağlantı',
    },
    Risk: {
        Code: 'Kod',
        Title: 'Risk tanımı',
        Description: 'Açıklama',
        Category: 'Kategori',
        Probability: 'Olasılık',
        Impact: 'Etki',
        Mitigation: 'Önleyici aksiyon',
        OwnerUserId: 'Sorumlu',
        IsSharedWithRequester: 'Talep Sahibi ile paylaşılır',
        Status: 'Durum',
    },
    ProjectAssessment: {
        Effort: 'Tahmini efor',
        ScreensAndModules: 'Modül / ekran sayısı',
        Integrations: 'Dış sistem entegrasyonu',
        UserRoles: 'Kullanıcı rolü ve kitlesi',
        DataSensitivity: 'Veri hassasiyeti (KVKK)',
        HardwareDependency: 'Donanım / cihaz bağımlılığı',
        StrategicFit: 'Stratejik uyum',
        CommercialValue: 'Ticari değer',
        TechnicalFeasibility: 'Teknik uygulanabilirlik',
        ResourceAndSchedule: 'Kaynak ve takvim uygunluğu',
        CustomerFit: 'Müşteri uygunluğu',
        RiskLevel: 'Risk düzeyi',
    },
};

/** Enum fields and their Turkish value names, per entity. */
const VALUE_LABELS: Partial<Record<AuditEntityType, Record<string, Record<string, string>>>> = {
    Project: {
        Source: REQUEST_SOURCE_LABELS,
        // Old records may still hold the channels used before Faz 4 (D1).
        RequestChannel: { ...REQUEST_CHANNEL_LABELS, Written: 'Yazılı', Verbal: 'Sözlü' },
        RequestType: REQUEST_TYPE_LABELS,
        Priority: PROJECT_PRIORITY_LABELS,
        Status: PROJECT_STATUS_LABELS,
    },
    OpenIssue: { Type: OPEN_ISSUE_TYPE_LABELS, Status: OPEN_ISSUE_STATUS_LABELS },
    Meeting: { Type: MEETING_TYPE_LABELS },
    Requirement: { Type: REQUIREMENT_TYPE_LABELS, Priority: REQUIREMENT_PRIORITY_LABELS, Status: REQUIREMENT_STATUS_LABELS },
    ProjectDocument: { Status: DOCUMENT_STATUS_LABELS },
    Risk: { Category: RISK_CATEGORY_LABELS, Status: RISK_STATUS_LABELS },
};

export function entityLabel(entityType: string): string {
    return AUDIT_ENTITY_LABELS[entityType as AuditEntityType] ?? OTHER_ENTITY_LABELS[entityType] ?? entityType;
}

export function fieldLabel(entityType: string, field: string): string {
    return FIELD_LABELS[entityType as AuditEntityType]?.[field] ?? OTHER_FIELD_LABELS[entityType]?.[field] ?? field;
}

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;
const DATE_TIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/;

/** A value as people read it: enum names in Turkish, dates as 12.11.2026, yes / no, "—" for empty. */
export function displayValue(entityType: string, field: string, value: string | null): string {
    if (value === null || value === '') {
        return '—';
    }

    const enumLabel = VALUE_LABELS[entityType as AuditEntityType]?.[field]?.[value] ?? OTHER_VALUE_LABELS[entityType]?.[field]?.[value];
    if (enumLabel) {
        return enumLabel;
    }

    if (value === 'true') return 'Evet';
    if (value === 'false') return 'Hayır';

    const people = participantsOf(entityType, field, value);
    if (people !== null) {
        return people;
    }

    const dateOnly = DATE_ONLY.exec(value);
    if (dateOnly) {
        return `${dateOnly[3]}.${dateOnly[2]}.${dateOnly[1]}`;
    }

    if (DATE_TIME.test(value)) {
        // Stored in UTC; shown in the user's time zone.
        const iso = /[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z';
        return new Date(iso).toLocaleString('tr-TR');
    }

    return value;
}

/**
 * Minutes' participants are stored as a JSON list: [{"Name":"Elif Yılmaz","Role":"İş Analisti"}].
 * Shown as "Elif Yılmaz (İş Analisti), Can Öztürk"; older minutes kept plain text, which is shown as it is.
 */
function participantsOf(entityType: string, field: string, value: string): string | null {
    if (entityType !== 'Meeting' || field !== 'Participants' || !value.startsWith('[')) {
        return null;
    }

    try {
        const list = JSON.parse(value) as Record<string, string | null>[];
        return list
            .map(p => {
                const name = p['Name'] ?? p['name'] ?? '';
                const role = p['Role'] ?? p['role'];
                return role ? `${name} (${role})` : name;
            })
            .join(', ');
    } catch {
        return null;
    }
}
