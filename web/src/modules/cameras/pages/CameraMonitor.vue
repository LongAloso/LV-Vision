<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { http, errorText, evidenceUrl } from '../../../core/api/http';
import type { Alert, Camera, Page, Scenario } from '../../../core/api/types';
import { can } from '../../../core/auth/session';
import { liveAlert, liveAlertTick, liveCameraTick } from '../../../core/websocket/live';

const router = useRouter();
const cameras = ref<Camera[]>([]);
const scenarios = ref<Scenario[]>([]);
const alerts = ref<Alert[]>([]);
const running = ref<string[]>([]);
const connected = ref<string[]>([]);
const previews = reactive<Record<string, string>>({});
const rows = ref(3); const columns = ref(4); const smooth = ref(true);
const sort = ref('name'); const error = ref(''); const busy = ref('');
let statusInterval: number | null = null;
let previewInterval: number | null = null;
let previewLoading = false;
const visible = computed(() => [...cameras.value].sort((a, b) => sort.value === 'fps'
  ? b.fps - a.fps : a.name.localeCompare(b.name)).slice(0, rows.value * columns.value));
function scenarioCount(cameraId: string): number {
  return scenarios.value.filter(item => item.cameraId === cameraId && item.enabled).length;
}
function poseEnabled(cameraId: string): boolean {
  return scenarios.value.some(item => item.cameraId === cameraId &&
    item.enabled && item.behaviorCode === 'PERSON_SITTING');
}
async function loadCameras() {
  try {
    const [cameraResult, scenarioResult, streamResult] = await Promise.all([
      http.get<Page<Camera>>('/cameraservice/cameras?pageSize=100'),
      http.get<Page<Scenario>>('/cameraservice/scenarios?pageSize=100'),
      http.get<{ items: { cameraId: string; running: boolean; connected: boolean }[] }>('/detect/sitting/streams')
        .catch(() => ({ data: { items: [] as { cameraId: string; running: boolean; connected: boolean }[] } })),
    ]);
    cameras.value = cameraResult.data.items;
    scenarios.value = scenarioResult.data.items;
    running.value = streamResult.data.items.filter(item => item.running).map(item => item.cameraId);
    connected.value = streamResult.data.items.filter(item => item.connected).map(item => item.cameraId);
  } catch (reason) { error.value = errorText(reason); }
}
async function loadAlerts() {
  try { alerts.value = (await http.get<Page<Alert>>('/alertservice/alerts?pageSize=10')).data.items; }
  catch { /* camera grid stays available */ }
}
async function loadPreviews() {
  if (previewLoading || document.hidden) return;
  previewLoading = true;
  try {
    await Promise.all(visible.value.map(async camera => {
      if (!connected.value.includes(camera.id)) return;
      try {
        const url = await evidenceUrl(`/detect/sitting/streams/${camera.id}/preview`);
        if (previews[camera.id]) URL.revokeObjectURL(previews[camera.id]);
        previews[camera.id] = url;
      } catch { /* the first RTSP frame may not have arrived yet */ }
    }));
  } finally {
    previewLoading = false;
  }
}
async function toggle(camera: Camera) {
  error.value = ''; busy.value = camera.id;
  try {
    await http.post(`/detect/sitting/streams/${running.value.includes(camera.id) ? 'stop' : 'start'}`,
      { cameraId: camera.id });
    await loadCameras();
  } catch (reason) { error.value = errorText(reason); }
  finally { busy.value = ''; }
}
watch(liveAlertTick, loadAlerts);
watch(liveCameraTick, loadCameras);
onMounted(async () => {
  await Promise.all([loadCameras(), loadAlerts()]);
  await loadPreviews();
  // Phase 2 RTSP workers are capped at 10 FPS. Poll at the same cadence so the
  // monitor can display every available frame without manufacturing duplicates.
  previewInterval = window.setInterval(loadPreviews, 100);
  statusInterval = window.setInterval(loadCameras, 2000);
});
onUnmounted(() => {
  if (previewInterval) window.clearInterval(previewInterval);
  if (statusInterval) window.clearInterval(statusInterval);
  Object.values(previews).forEach(URL.revokeObjectURL);
});
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">GIÁM SÁT / CAMERA</div><h1>Giám sát camera</h1><p>Ảnh xem nhanh và thông số phân tích theo thời gian thực.</p></div><RouterLink v-if="can('Camera.Manage')" class="primary-button" to="/camera-management?add=1"><i class="pi pi-plus" /> Quản lý camera</RouterLink></section>
  <div v-if="error" class="error-box" role="alert">{{ error }}</div>
  <div class="info-box">Xương và đường vùng hiện trên camera đang chạy khi đã bật kịch bản “Ngồi ở lối đi” và khung hình có người được model nhận ra. Camera chưa gán kịch bản vẫn hiển thị ảnh gốc.</div>
  <div class="monitor-layout">
    <section class="monitor-main">
      <div class="surface toolbar"><strong><i class="pi pi-th-large" /> Lưới camera</strong><div class="toolbar-controls"><label>Hàng <select v-model.number="rows"><option v-for="n in 4" :key="n">{{ n }}</option></select></label><label>Cột <select v-model.number="columns"><option v-for="n in 4" :key="n">{{ n }}</option></select></label><label class="switch-label"><input v-model="smooth" type="checkbox" /> Mượt</label><label>Sắp xếp <select v-model="sort"><option value="name">Tên</option><option value="fps">FPS</option></select></label><span class="subtle">{{ cameras.length }} camera</span></div></div>
      <div class="camera-grid" :class="{ smooth }" :style="{ '--columns': columns }">
        <article v-for="camera in visible" :key="camera.id" class="camera-tile">
          <div class="video-box"><img v-if="previews[camera.id] && connected.includes(camera.id)" :src="previews[camera.id]" :alt="`Khung hình ${camera.name}`" /><div v-else class="video-placeholder"><i class="pi pi-video" /><span>{{ running.includes(camera.id) ? 'Đang kết nối camera' : 'Chưa phát luồng' }}</span></div><span class="live-chip" :class="connected.includes(camera.id) ? 'online' : 'offline'">{{ connected.includes(camera.id) ? '● LIVE' : running.includes(camera.id) ? '● KẾT NỐI' : '● OFFLINE' }}</span><span v-if="connected.includes(camera.id)" class="pose-chip">{{ poseEnabled(camera.id) ? 'POSE + VÙNG' : 'ẢNH GỐC' }}</span></div>
          <div class="camera-meta"><strong>{{ camera.name }}</strong><span>{{ camera.fps.toFixed(1) }}/{{ camera.targetFps }} FPS</span></div>
          <div class="camera-sub"><span>{{ scenarioCount(camera.id) }} kịch bản bật</span><button class="text-button" @click="router.push({ path: '/scenarios', query: { cameraId: camera.id } })">Cấu hình</button><button v-if="can('Camera.Manage')" class="text-button" :disabled="busy === camera.id" @click="toggle(camera)">{{ running.includes(camera.id) ? 'Dừng' : 'Bắt đầu' }}</button></div>
        </article>
        <div v-if="!cameras.length" class="empty-grid"><i class="pi pi-video" /><h3>Chưa có camera</h3><p>Thêm thiết bị ở mục Camera, sau đó chọn hành vi ở Kịch bản & Camera AI.</p></div>
      </div>
      <section class="surface"><div class="card-heading"><h2>Tình trạng camera</h2><span>FPS, độ trễ và worker</span></div><div class="table-scroll"><table><thead><tr><th>Camera</th><th>Trạng thái</th><th>FPS thực tế / mục tiêu</th><th>p50 / p95</th><th>Worker</th><th>Kịch bản</th></tr></thead><tbody><tr v-for="camera in cameras" :key="camera.id"><td><strong>{{ camera.name }}</strong></td><td><span class="status-pill" :class="connected.includes(camera.id) ? 'ok' : running.includes(camera.id) ? 'warning' : 'muted'">{{ connected.includes(camera.id) ? 'Hoạt động' : running.includes(camera.id) ? 'Đang kết nối' : 'Chưa chạy' }}</span></td><td>{{ camera.fps.toFixed(1) }} / {{ camera.targetFps }}</td><td>{{ camera.p50Ms.toFixed(0) }} / {{ camera.p95Ms.toFixed(0) }} ms</td><td>{{ camera.workerId || '—' }}</td><td><button class="text-button" @click="router.push({ path: '/scenarios', query: { cameraId: camera.id } })">{{ scenarioCount(camera.id) }} kịch bản</button></td></tr><tr v-if="!cameras.length"><td colspan="6" class="empty">Chưa có camera.</td></tr></tbody></table></div></section>
    </section>
    <aside class="surface live-sidebar"><div class="card-heading"><h2>Cảnh báo hành vi trực tiếp</h2><span class="live-indicator">● LIVE</span></div><p class="subtle">{{ liveAlert?.title ? `Mới nhất: ${liveAlert.title}` : 'Cập nhật khi detector phát hiện hành vi.' }}</p><button v-for="item in alerts" :key="item.id" class="live-alert" @click="router.push({ path: '/alerts', query: { id: item.id } })"><span class="severity-dot" :class="item.severity.toLowerCase()" /><span><strong>{{ item.behaviorName }}</strong><small>{{ item.cameraName }} · {{ item.zoneId }}</small><small>{{ new Date(item.detectedAt).toLocaleString('vi-VN') }}</small></span></button><p v-if="!alerts.length" class="empty">Chưa có cảnh báo.</p></aside>
  </div>
</template>
