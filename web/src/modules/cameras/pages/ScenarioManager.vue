<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { http, errorText, evidenceUrl } from '../../../core/api/http';
import type { Behavior, Camera, DetectorCapability, Page, Scenario } from '../../../core/api/types';
import { can } from '../../../core/auth/session';

type Point = [number, number];
const route = useRoute();
const router = useRouter();
const cameras = ref<Camera[]>([]);
const behaviors = ref<Behavior[]>([]);
const capabilities = ref<DetectorCapability[]>([]);
const scenarios = ref<Scenario[]>([]);
const selectedId = ref('');
const running = ref<string[]>([]);
const error = ref('');
const busy = ref(false);
const togglingBehavior = ref('');
const editor = ref<{ behavior: Behavior; scenario: Scenario | null } | null>(null);
const draft = reactive({ zoneId: '', points: [] as Point[] });
const background = ref('');
const imageSize = reactive({ width: 0, height: 0 });
const previewLoading = ref(false);
const previewMessage = ref('');
const frameWarning = ref('');
const draggingIndex = ref<number | null>(null);
let ownedBackground = '';
let previewRequestId = 0;

const selectedCamera = computed(() => cameras.value.find(item => item.id === selectedId.value));
const selectedScenarios = computed(() => scenarios.value.filter(item => item.cameraId === selectedId.value));
const polygonPoints = computed(() => draft.points.map(([x, y]) => `${x},${y}`).join(' '));
const markerRadius = computed(() => Math.max(7, imageSize.width / 120));

function byBehavior(code: string) {
  return selectedScenarios.value.filter(item => item.behaviorCode === code);
}
function capability(code: string) {
  return capabilities.value.find(item => item.behaviorCode === code);
}
function scenarioBody(item: Scenario, enabled = item.enabled) {
  return {
    cameraId: item.cameraId, behaviorCode: item.behaviorCode, behaviorVersion: item.behaviorVersion,
    zoneId: item.zoneId, zonePolygon: item.zonePolygon, zoneMode: item.zoneMode,
    detectorUrl: item.detectorUrl, enabled,
  };
}
function behaviorBody(item: Behavior, isActive = item.isActive) {
  return {
    code: item.code, nameVi: item.nameVi, nameEn: item.nameEn,
    detectTemplate: item.detectTemplate, severity: item.severity,
    zoneType: item.zoneType, parameters: item.parameters, isActive,
  };
}
async function load() {
  try {
    const [cameraResult, behaviorResult, detectorResult, streamResult] = await Promise.all([
      http.get<Page<Camera>>('/cameraservice/cameras?pageSize=100'),
      http.get<Page<Behavior>>('/alertservice/behaviors?pageSize=100'),
      http.get<{ items: DetectorCapability[] }>('/cameraservice/scenarios/detectors'),
      http.get<{ items: { cameraId: string; running: boolean }[] }>('/detect/sitting/streams')
        .catch(() => ({ data: { items: [] as { cameraId: string; running: boolean }[] } })),
    ]);
    cameras.value = cameraResult.data.items;
    behaviors.value = behaviorResult.data.items;
    capabilities.value = detectorResult.data.items;
    running.value = streamResult.data.items.filter(item => item.running).map(item => item.cameraId);
    const requested = String(route.query.cameraId || '');
    selectedId.value = cameras.value.some(item => item.id === requested) ? requested : cameras.value[0]?.id || '';
    await loadScenarios();
  } catch (cause) { error.value = errorText(cause); }
}
async function loadScenarios() {
  scenarios.value = selectedId.value
    ? (await http.get<Page<Scenario>>(`/cameraservice/scenarios?cameraId=${encodeURIComponent(selectedId.value)}&pageSize=100`)).data.items
    : [];
}
async function selectCamera() {
  error.value = '';
  await router.replace({ query: selectedId.value ? { cameraId: selectedId.value } : {} });
  try { await loadScenarios(); } catch (cause) { error.value = errorText(cause); }
}
async function restartIfRunning() {
  if (!running.value.includes(selectedId.value)) return;
  await http.post('/detect/sitting/streams/stop', { cameraId: selectedId.value });
  await http.post('/detect/sitting/streams/start', { cameraId: selectedId.value });
}
async function toggleBehavior(behavior: Behavior, enabled: boolean) {
  if (!can('Camera.Manage') || busy.value) return;
  const existing = byBehavior(behavior.code);
  if (enabled && !existing.length) { await openEditor(behavior); return; }
  error.value = '';
  busy.value = true;
  togglingBehavior.value = behavior.code;
  try {
    if (enabled && !behavior.isActive) {
      if (!can('Behavior.Manage')) {
        throw new Error('Hành vi đã tắt trong danh mục. Cần quyền Behavior.Manage để bật lại.');
      }
      const { data: activeBehavior } = await http.put<Behavior>(
        `/alertservice/behaviors/${behavior.id}`, behaviorBody(behavior, true));
      behaviors.value = behaviors.value.map(item => item.code === behavior.code ? activeBehavior : item);
    }
    for (const item of existing) {
      if (item.enabled !== enabled) {
        await http.put(`/cameraservice/scenarios/${item.id}`, scenarioBody(item, enabled));
      }
    }
    await restartIfRunning();
    await loadScenarios();
  } catch (cause) { error.value = errorText(cause); await loadScenarios(); }
  finally { busy.value = false; togglingBehavior.value = ''; }
}
function clearBackground() {
  if (ownedBackground) URL.revokeObjectURL(ownedBackground);
  ownedBackground = '';
  background.value = '';
  imageSize.width = 0;
  imageSize.height = 0;
  frameWarning.value = '';
}
function closeEditor() {
  previewRequestId++;
  previewLoading.value = false;
  editor.value = null;
  draggingIndex.value = null;
  clearBackground();
}
async function captureFrame() {
  if (!editor.value || !selectedId.value || previewLoading.value) return;
  const cameraId = selectedId.value;
  const requestId = ++previewRequestId;
  previewLoading.value = true;
  previewMessage.value = '';
  try {
    for (let attempt = 0; attempt < 12; attempt++) {
      if (requestId !== previewRequestId || !editor.value) return;
      try {
        // The editor uses a clean frame; the monitor uses the annotated preview.
        const url = await evidenceUrl(`/detect/sitting/streams/${cameraId}/preview?overlay=false`);
        if (requestId !== previewRequestId || !editor.value || selectedId.value !== cameraId) {
          URL.revokeObjectURL(url);
          return;
        }
        if (ownedBackground) URL.revokeObjectURL(ownedBackground);
        ownedBackground = url;
        background.value = url;
        return;
      } catch (cause) {
        if (!axios.isAxiosError(cause) || cause.response?.status !== 404) {
          previewMessage.value = errorText(cause);
          return;
        }
        await new Promise<void>(resolve => window.setTimeout(resolve, 750));
      }
    }
    previewMessage.value = 'Camera chưa gửi khung hình. Kiểm tra RTSP rồi thử lấy ảnh lại.';
  } finally {
    if (requestId === previewRequestId) previewLoading.value = false;
  }
}
async function openEditor(behavior: Behavior, scenario: Scenario | null = null) {
  if (!can('Camera.Manage') || !capability(behavior.code)) return;
  closeEditor();
  error.value = '';
  editor.value = { behavior, scenario };
  draft.zoneId = scenario?.zoneId || `zone-${byBehavior(behavior.code).length + 1}`;
  draft.points = scenario?.zonePolygon.map(([x, y]) => [x, y] as Point) || [];
  if (running.value.includes(selectedId.value)) await captureFrame();
  else previewMessage.value = 'Bật camera để lấy khung hình và đặt các điểm vùng.';
}
function uploadImage(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  previewRequestId++;
  previewLoading.value = false;
  clearBackground();
  ownedBackground = URL.createObjectURL(file);
  background.value = ownedBackground;
  previewMessage.value = '';
}
function onImageLoad(event: Event) {
  const image = event.target as HTMLImageElement;
  if (imageSize.width && imageSize.height && draft.points.length &&
      (imageSize.width !== image.naturalWidth || imageSize.height !== image.naturalHeight)) {
    frameWarning.value = 'Độ phân giải ảnh đã đổi. Hãy vẽ lại vùng để tọa độ khớp camera.';
  }
  imageSize.width = image.naturalWidth;
  imageSize.height = image.naturalHeight;
}
function imagePoint(event: MouseEvent | PointerEvent, svg: SVGSVGElement): Point {
  const rect = svg.getBoundingClientRect();
  return [
    Math.max(0, Math.min(imageSize.width,
      Math.round((event.clientX - rect.left) * imageSize.width / rect.width))),
    Math.max(0, Math.min(imageSize.height,
      Math.round((event.clientY - rect.top) * imageSize.height / rect.height))),
  ];
}
function addPoint(event: MouseEvent) {
  if (!imageSize.width || !imageSize.height || event.target !== event.currentTarget) return;
  draft.points.push(imagePoint(event, event.currentTarget as SVGSVGElement));
}
function startDrag(event: PointerEvent, index: number) {
  draggingIndex.value = index;
  (event.currentTarget as SVGCircleElement).setPointerCapture(event.pointerId);
}
function movePoint(event: PointerEvent) {
  if (draggingIndex.value === null || !imageSize.width || !imageSize.height) return;
  draft.points[draggingIndex.value] = imagePoint(event, event.currentTarget as SVGSVGElement);
}
function stopDrag() { draggingIndex.value = null; }
function validArea(points: Point[]) {
  return Math.abs(points.reduce((sum, [x, y], i) => {
    const [nextX, nextY] = points[(i + 1) % points.length];
    return sum + x * nextY - nextX * y;
  }, 0)) > 1;
}
async function saveZone() {
  if (!editor.value || !selectedId.value) return;
  if (!background.value || frameWarning.value) {
    error.value = 'Hãy lấy khung hình đúng độ phân giải camera và vẽ lại vùng.';
    return;
  }
  if (draft.points.length < 3 || !validArea(draft.points)) {
    error.value = 'Vùng cần ít nhất ba điểm không thẳng hàng.';
    return;
  }
  const { behavior, scenario } = editor.value;
  const detector = capability(behavior.code);
  if (!detector) return;
  const zoneId = draft.zoneId.trim();
  if (!zoneId) { error.value = 'Hãy đặt tên vùng.'; return; }
  if (selectedScenarios.value.some(item =>
    item.id !== scenario?.id && item.behaviorCode === behavior.code && item.zoneId === zoneId)) {
    error.value = 'Tên vùng đã dùng cho hành vi này.';
    return;
  }
  busy.value = true;
  error.value = '';
  try {
    const body = {
      cameraId: selectedId.value, behaviorCode: behavior.code,
      behaviorVersion: scenario?.behaviorVersion || behavior.version, zoneId,
      zonePolygon: draft.points, zoneMode: 'ANY', detectorUrl: detector.detectorUrl,
      enabled: scenario?.enabled ?? true,
    };
    if (scenario) await http.put(`/cameraservice/scenarios/${scenario.id}`, body);
    else await http.post('/cameraservice/scenarios', body);
    await restartIfRunning();
    await loadScenarios();
    closeEditor();
  } catch (cause) { error.value = errorText(cause); await loadScenarios(); }
  finally { busy.value = false; }
}
async function removeZone(item: Scenario) {
  if (!confirm(`Xóa vùng ${item.zoneId} của ${item.behaviorCode}?`)) return;
  busy.value = true;
  error.value = '';
  try {
    await http.delete(`/cameraservice/scenarios/${item.id}`);
    await restartIfRunning();
    await loadScenarios();
  } catch (cause) { error.value = errorText(cause); await loadScenarios(); }
  finally { busy.value = false; }
}
async function startPreview() {
  if (!selectedId.value) return;
  busy.value = true;
  error.value = '';
  try {
    await http.post('/detect/sitting/streams/start', { cameraId: selectedId.value });
    if (!running.value.includes(selectedId.value)) running.value.push(selectedId.value);
    if (editor.value) await captureFrame();
  } catch (cause) { error.value = errorText(cause); }
  finally { busy.value = false; }
}
watch(() => route.query.cameraId, async value => {
  if (cameras.value.some(item => item.id === value) && selectedId.value !== value) {
    selectedId.value = String(value);
    await loadScenarios();
  }
});
onMounted(load);
onUnmounted(closeEditor);
</script>

<template>
  <section class="page-head">
    <div><div class="eyebrow">CẤU HÌNH PHÂN TÍCH</div><h1>Kịch bản & Camera AI</h1><p>Chọn hành vi và đặt từng điểm vùng lên khung hình camera.</p></div>
    <RouterLink class="secondary-button" to="/camera-management">Quản lý camera</RouterLink>
  </section>
  <div v-if="error" class="error-box" role="alert">{{ error }}</div>
  <div v-if="!cameras.length" class="info-box">Chưa có camera. <RouterLink to="/camera-management">Thêm camera</RouterLink> trước khi cấu hình kịch bản.</div>
  <template v-else>
    <div class="surface filter-bar compact">
      <label>Camera<select v-model="selectedId" @change="selectCamera"><option v-for="camera in cameras" :key="camera.id" :value="camera.id">{{ camera.name }}</option></select></label>
      <span class="status-pill" :class="running.includes(selectedId) ? 'ok' : 'muted'">{{ running.includes(selectedId) ? 'Luồng đang chạy' : 'Luồng chưa chạy' }}</span>
      <button v-if="can('Camera.Manage') && !running.includes(selectedId)" class="secondary-button" :disabled="busy" @click="startPreview">Bật camera</button>
    </div>
    <div class="info-box">Camera mới chưa gán hành vi nào. Mỗi hành vi có thể có nhiều vùng. Chỉ mục có detector đã đăng ký mới bật được; thêm mục trong Danh mục hành vi chưa tạo ra model AI.</div>
    <div class="surface">
      <div class="card-heading"><h2>Hành vi của {{ selectedCamera?.name }}</h2><span>{{ selectedScenarios.filter(item => item.enabled).length }} vùng đang bật</span></div>
      <div class="table-scroll"><table>
        <thead><tr><th>Giám sát</th><th>Hành vi</th><th>Detector</th><th>Vùng đã tạo</th><th>Thao tác</th></tr></thead>
        <tbody>
          <tr v-for="behavior in behaviors" :key="behavior.code">
            <td><button type="button" class="scenario-toggle" :class="{ enabled: byBehavior(behavior.code).some(item => item.enabled) }" :disabled="!can('Camera.Manage') || busy || (!capability(behavior.code) && !byBehavior(behavior.code).some(item => item.enabled)) || (!behavior.isActive && !can('Behavior.Manage') && !byBehavior(behavior.code).some(item => item.enabled))" :aria-pressed="byBehavior(behavior.code).some(item => item.enabled)" :title="!behavior.isActive ? 'Bật lại hành vi trong danh mục rồi bật các vùng' : ''" @click="toggleBehavior(behavior, !byBehavior(behavior.code).some(item => item.enabled))"><span aria-hidden="true" />{{ togglingBehavior === behavior.code ? 'Đang lưu…' : byBehavior(behavior.code).some(item => item.enabled) ? 'Bật' : !behavior.isActive ? 'Bật lại' : 'Tắt' }}</button></td>
            <td><strong>{{ behavior.nameVi }}</strong><small>{{ behavior.code }} · {{ behavior.version }}{{ behavior.isActive ? '' : ' · đã tắt trong danh mục' }}</small></td>
            <td><span class="status-pill" :class="capability(behavior.code) ? 'ok' : 'muted'">{{ capability(behavior.code) ? 'Đã đăng ký' : 'Chưa có detector' }}</span></td>
            <td>
              <div v-for="item in byBehavior(behavior.code)" :key="item.id" class="zone-line">
                <span>{{ item.zoneId }} <small>({{ item.enabled ? 'bật' : 'tắt' }})</small></span>
                <button v-if="can('Camera.Manage')" class="text-button" @click="openEditor(behavior, item)">Sửa vùng</button>
                <button v-if="can('Camera.Manage')" class="text-button danger" :disabled="busy" @click="removeZone(item)">Xóa</button>
              </div>
              <span v-if="!byBehavior(behavior.code).length" class="subtle">Chưa có vùng</span>
            </td>
            <td><button v-if="can('Camera.Manage') && behavior.isActive && capability(behavior.code)" class="text-button" @click="openEditor(behavior)">+ Thêm vùng</button></td>
          </tr>
          <tr v-if="!behaviors.length"><td colspan="5" class="empty">Danh mục hành vi chưa có mục nào.</td></tr>
        </tbody>
      </table></div>
    </div>
  </template>

  <div v-if="editor" class="modal-backdrop" @click.self="closeEditor">
    <form class="modal zone-modal" @submit.prevent="saveZone">
      <div class="card-heading">
        <h2>{{ editor.scenario ? 'Sửa vùng' : 'Thêm vùng' }} · {{ editor.behavior.nameVi }}</h2>
        <button type="button" class="icon-button" aria-label="Đóng" @click="closeEditor"><i class="pi pi-times" /></button>
      </div>
      <div class="form-row"><label>Tên vùng<input v-model="draft.zoneId" maxlength="80" required /></label><span class="subtle">zoneMode: ANY</span></div>
      <div class="form-row zone-source-actions">
        <button v-if="!running.includes(selectedId)" type="button" class="secondary-button" :disabled="busy" @click="startPreview">Bật camera và lấy khung hình</button>
        <button v-else type="button" class="secondary-button" :disabled="previewLoading" @click="captureFrame">{{ previewLoading ? 'Đang lấy khung hình...' : 'Chụp khung hình mới' }}</button>
        <span class="subtle">Ảnh được giữ cố định khi vẽ; chỉ lưu tọa độ, không tải ảnh lên server.</span>
      </div>
      <p v-if="previewMessage" class="info-box">{{ previewMessage }}</p>
      <p v-if="frameWarning" class="error-box">{{ frameWarning }}</p>
      <div v-if="background" class="zone-canvas">
        <img :src="background" alt="Khung hình camera để đặt điểm vùng" @load="onImageLoad" />
        <svg v-if="imageSize.width && imageSize.height" :viewBox="`0 0 ${imageSize.width} ${imageSize.height}`" preserveAspectRatio="none"
             aria-label="Nhấp để thêm điểm vùng, kéo điểm để chỉnh" @click="addPoint" @pointermove="movePoint" @pointerup="stopDrag" @pointercancel="stopDrag">
          <polygon v-if="draft.points.length >= 3" :points="polygonPoints" />
          <polyline :points="polygonPoints" />
          <template v-for="([x, y], index) in draft.points" :key="index">
            <circle :cx="x" :cy="y" :r="markerRadius" @pointerdown.stop.prevent="startDrag($event, index)" @click.stop />
            <text class="zone-point-label" :x="x + markerRadius" :y="y - markerRadius" :font-size="markerRadius * 2">{{ index + 1 }}</text>
          </template>
        </svg>
      </div>
      <div v-else class="info-box">Chưa có khung hình. Bật camera hoặc chọn ảnh chụp từ chính camera bên dưới.</div>
      <p class="subtle">Nhấp các đỉnh theo thứ tự trên ảnh; kéo một điểm để sửa. Tọa độ lưu theo độ phân giải gốc {{ imageSize.width && imageSize.height ? `${imageSize.width} × ${imageSize.height}` : 'của camera' }}.</p>
      <label>Ảnh chụp từ chính camera (khi RTSP chưa sẵn sàng)<input type="file" accept="image/*" @change="uploadImage" /></label>
      <p class="subtle">Ảnh tải từ máy phải cùng độ phân giải và góc nhìn với luồng camera sẽ giám sát.</p>
      <div class="form-row zone-controls">
        <span>{{ draft.points.length }} điểm</span>
        <button type="button" class="secondary-button" :disabled="!draft.points.length" @click="draft.points.pop()">Lùi một điểm</button>
        <button type="button" class="secondary-button" :disabled="!draft.points.length" @click="draft.points = []; frameWarning = ''">Vẽ lại</button>
      </div>
      <button class="primary-button" :disabled="busy || !background || !!frameWarning || draft.points.length < 3">Lưu vùng</button>
    </form>
  </div>
</template>
