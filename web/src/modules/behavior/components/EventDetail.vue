<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { http, errorText, evidenceUrl } from '../../../core/api/http';
import type { Alert } from '../../../core/api/types';
import { can } from '../../../core/auth/session';

const props = defineProps<{ alertId: string | null; refreshTick: number }>();
const emit = defineEmits<{ close: []; changed: [] }>();
const alert = ref<Alert | null>(null);
const snapshot = ref(''); const clip = ref('');
const assignee = ref(''); const reason = ref('');
const note = ref({ description: '', person: '', result: '' });
const error = ref(''); const busy = ref(false);
function clearMedia() {
  if (snapshot.value) URL.revokeObjectURL(snapshot.value);
  if (clip.value) URL.revokeObjectURL(clip.value);
  snapshot.value = ''; clip.value = '';
}
async function load() {
  if (!props.alertId) { alert.value = null; clearMedia(); return; }
  try {
    const { data } = await http.get<Alert>(`/alertservice/alerts/${props.alertId}`);
    const oldSnapshot = alert.value?.snapshotUrl;
    const oldClip = alert.value?.clipUrl;
    alert.value = data; assignee.value = data.assignee || ''; reason.value = data.reason || '';
    if (data.snapshotUrl !== oldSnapshot || !snapshot.value) {
      if (snapshot.value) URL.revokeObjectURL(snapshot.value);
      snapshot.value = await evidenceUrl(data.snapshotUrl);
    }
    if (data.clipUrl && (data.clipUrl !== oldClip || !clip.value)) {
      if (clip.value) URL.revokeObjectURL(clip.value);
      clip.value = await evidenceUrl(data.clipUrl);
    }
  } catch (cause) { error.value = errorText(cause); }
}
async function action(kind: 'assign' | 'acknowledge' | 'close' | 'mark-false') {
  if (!alert.value) return;
  busy.value = true; error.value = '';
  try {
    const body = kind === 'assign' ? { assignee: assignee.value } : kind === 'close' ? { reason: reason.value } : undefined;
    await http.put(`/alertservice/alerts/${alert.value.id}/${kind}`, body);
    await load(); emit('changed');
  } catch (cause) { error.value = errorText(cause); }
  finally { busy.value = false; }
}
async function addNote() {
  if (!alert.value) return;
  busy.value = true; error.value = '';
  try {
    await http.post(`/alertservice/alerts/${alert.value.id}/remediation`, note.value);
    note.value = { description: '', person: '', result: '' };
    await load(); emit('changed');
  } catch (cause) { error.value = errorText(cause); }
  finally { busy.value = false; }
}
watch(() => [props.alertId, props.refreshTick], load, { immediate: true });
onUnmounted(clearMedia);
</script>

<template>
  <aside v-if="alertId" class="detail-panel"><div class="detail-top"><div><span class="eyebrow">CHI TIẾT CẢNH BÁO</span><h2>{{ alert?.behaviorName }} {{ alert?.behaviorVersion }}</h2><p>{{ alert?.cameraName }} · {{ alert?.placeName || 'Chưa gán địa điểm' }}</p></div><button class="icon-button" aria-label="Đóng chi tiết" @click="emit('close')"><i class="pi pi-times" /></button></div>
    <div v-if="error" class="error-box">{{ error }}</div>
    <template v-if="alert">
      <div class="detail-scroll"><div class="detail-photo"><img v-if="snapshot" :src="snapshot" alt="Ảnh minh chứng có bbox và skeleton" /><div v-else class="empty">Đang tải ảnh minh chứng...</div></div>
        <div class="clip-box"><video v-if="clip" :src="clip" controls /><p v-else-if="alert.clipStatus === 'PENDING'"><i class="pi pi-spin pi-spinner" /> Đang cắt clip minh chứng...</p><p v-else>Camera không bật ghi video hoặc không có frame để cắt clip.</p></div>
        <div class="detail-section"><h3>Thông số phát hiện</h3><div class="metric-grid"><div><span>Trạng thái</span><strong>{{ alert.status }}</strong></div><div><span>Độ tin cậy</span><strong>{{ (alert.confidence * 100).toFixed(1) }}%</strong></div><div><span>Tư thế</span><strong>{{ alert.posture }}</strong></div><div><span>Vùng</span><strong>{{ alert.zoneId }}</strong></div><div><span>Thời điểm</span><strong>{{ new Date(alert.detectedAt).toLocaleString('vi-VN') }}</strong></div><div><span>Bbox</span><strong>{{ alert.bbox.join(', ') }}</strong></div><div v-for="(value, key) in alert.metrics" :key="key"><span>{{ key }}</span><strong>{{ typeof value === 'object' ? JSON.stringify(value) : value }}</strong></div></div></div>
        <div class="detail-section"><h3>Xử lý</h3><div class="form-row"><label>Người phụ trách<input v-model="assignee" :disabled="!can('Alert.Manage')" /></label><button class="primary-button" :disabled="busy || !assignee.trim() || !can('Alert.Manage')" @click="action('assign')">Giao</button></div><div class="form-row"><button class="secondary-button" :disabled="busy || !can('Alert.Manage')" @click="action('acknowledge')">Xác nhận</button><button class="secondary-button" :disabled="busy || !can('Alert.Manage')" @click="action('mark-false')">Đánh dấu sai</button></div><label>Lý do / kết luận<textarea v-model="reason" rows="2" :disabled="!can('Alert.Manage')" /></label><button class="primary-button" :disabled="busy || !reason.trim() || !can('Alert.Manage')" @click="action('close')">Đóng cảnh báo</button></div>
        <div class="detail-section"><h3>Hành động khắc phục</h3><div v-for="item in alert.remediations" :key="item.id" class="remediation-item"><strong>{{ item.description }}</strong><small>{{ item.person }} · {{ item.result }}</small></div><form v-if="can('Alert.Manage')" class="stack" @submit.prevent="addNote"><label>Mô tả<input v-model="note.description" required /></label><label>Người làm<input v-model="note.person" required /></label><label>Kết quả<input v-model="note.result" required /></label><button class="secondary-button" :disabled="busy">Thêm hành động</button></form></div>
      </div>
    </template>
  </aside>
</template>
