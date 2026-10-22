export interface Page<T> { items: T[]; page: number; pageSize: number; total: number }
export interface Camera {
  id: string; name: string; placeId: string | null; placeName?: string | null;
  isActive: boolean; targetFps: number; fps: number; p50Ms: number; p95Ms: number;
  workerId: string | null; statusUpdatedAt: string; referenceFrameUrl: string | null;
}
export interface Place { id: string; name: string }
export interface Scenario {
  id: string; cameraId: string; behaviorCode: string; behaviorVersion: string;
  zoneId: string; zonePolygon: number[][]; zoneMode: 'ANY' | 'ALL';
  detectorUrl: string; enabled: boolean;
}
export interface DetectorCapability { behaviorCode: string; detectorUrl: string }
export interface Alert {
  id: string; title: string; cameraId: string; cameraName: string; placeId: string | null;
  placeName: string | null; behaviorCode: string; behaviorVersion: string; behaviorName: string;
  severity: 'INFO' | 'WARNING' | 'CRITICAL'; zoneId: string; confidence: number;
  posture: string; bbox: number[]; skeleton: number[][]; metrics: Record<string, unknown>;
  snapshotUrl: string; clipUrl: string | null; clipStatus: string; detectedAt: string;
  status: string; assignee: string | null; reason: string | null;
  remediations?: Remediation[];
}
export interface Remediation { id: string; alertId: string; description: string; person: string; result: string; createdAt: string }
export interface Behavior {
  id: string; code: string; nameVi: string; nameEn: string; detectTemplate: string;
  severity: 'INFO' | 'WARNING' | 'CRITICAL'; zoneType: string; version: string;
  isActive: boolean; isSystem: boolean; parameters: Record<string, unknown>;
}
export interface Notification { id: string; alertId: string; title: string; severity: string; createdAt: string; isRead: boolean }
export interface Health {
  services: { name: string; status: string; detail: string; latencyMs: number | null }[];
  cameras: Camera[]; workers: { cameraId: string; state?: string; fps?: number }[];
  redisQueues: { name: string; length: number | null }[]; plcDevices: unknown[];
  resources: { scope: string; cpuPercent: number; memoryPercent: number; diskPercent: number; gpu: unknown };
  storage: { retentionDays: number; usedBytes: number; freeBytes: number; estimated30DayBytes: number | null; cleanupEnabled: boolean };
}
export interface ReportSummary {
  from: string; to: string; timezone: 'UTC'; total: number; open: number;
  critical: number; falsePositive: number;
  daily: { day: string; count: number }[];
  bySeverity: { severity: string; count: number }[];
  byStatus: { status: string; count: number }[];
  byBehavior: { code: string; name: string; count: number }[];
  byCamera: { id: string; name: string; count: number }[];
}
