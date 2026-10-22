<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { http, errorText } from '../../../core/api/http';
import type { Alert, Behavior, Camera, Health, Page, Place } from '../../../core/api/types';
import { liveAlertTick } from '../../../core/websocket/live';
import EventDetail from '../components/EventDetail.vue';

const route = useRoute(); const router = useRouter();
const alerts = ref<Alert[]>([]); const total = ref(0); const page = ref(1);
const cameras = ref<Camera[]>([]); const places = ref<Place[]>([]); const behaviors = ref<Behavior[]>([]);
const selected = ref<string | null>(null); const detailTick = ref(0);
const storage = ref<Health['storage'] | null>(null);
const loading = ref(false); const error = ref('');
const filters = reactive({ status: '', cameraId: '', placeId: '', behaviorCode: '', severity: '', from: '', to: '', q: '' });
function formatBytes(bytes: number) { return `${(bytes / (1024 ** 3)).toFixed(1)} GB`; }
async function load() {
  loading.value = true; error.value = '';
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: '10' });
    for (const [key, value] of Object.entries(filters)) if (value) {
      params.set(key, (key === 'from' || key === 'to') ? new Date(value).toISOString() : value);
    }
    const { data } = await http.get<Page<Alert>>(`/alertservice/alerts?${params}`);
    alerts.value = data.items; total.value = data.total;
  } catch (cause) { error.value = errorText(cause); }
  finally { loading.value = false; }
}
function applyFilters() { page.value = 1; load(); }
function select(id: string) { selected.value = id; router.replace({ query: { ...route.query, id } }); }
function closeDetail() { selected.value = null; router.replace({ query: {} }); }
watch(() => route.query.id, id => { selected.value = typeof id === 'string' ? id : null; }, { immediate: true });
watch(liveAlertTick, () => { load(); detailTick.value++; });
onMounted(async () => {
  await load();
  const [cameraResult, placeResult, behaviorResult, healthResult] = await Promise.allSettled([
    http.get<Page<Camera>>('/cameraservice/cameras?pageSize=100'),
    http.get<Page<Place>>('/cameraservice/places?pageSize=100'),
    http.get<Page<Behavior>>('/alertservice/behaviors?pageSize=100'),
    http.get<Health>('/systemservice/system/health'),
  ]);
  if (cameraResult.status === 'fulfilled') cameras.value = cameraResult.value.data.items;
  if (placeResult.status === 'fulfilled') places.value = placeResult.value.data.items;
  if (behaviorResult.status === 'fulfilled') behaviors.value = behaviorResult.value.data.items;
  if (healthResult.status === 'fulfilled') storage.value = healthResult.value.data.storage;
});
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">GIÁM SÁT / CẢNH BÁO</div><h1>Sự kiện & cảnh báo</h1><p>Lọc, xem minh chứng và giao việc xử lý.</p></div><span class="count-chip">{{ total }} sự kiện</span></section>
  <div v-if="storage" class="quota-line"><i class="pi pi-database" /> Lưu trữ minh chứng: mục tiêu {{ storage.retentionDays }} ngày · đang dùng {{ formatBytes(storage.usedBytes) }} · còn trống {{ formatBytes(storage.freeBytes) }} · ước tính 30 ngày: {{ storage.estimated30DayBytes === null ? 'chưa đủ dữ liệu' : formatBytes(storage.estimated30DayBytes) }}<span v-if="!storage.cleanupEnabled"> · dọn tự động chưa bật</span></div>
  <div v-if="error" class="error-box" role="alert">{{ error }}</div>
  <div class="surface"><form class="filter-bar" @submit.prevent="applyFilters"><label>Trạng thái<select v-model="filters.status"><option value="">Tất cả</option><option value="NEW">Mới</option><option value="ASSIGNED">Đã giao</option><option value="ACKNOWLEDGED">Đã xác nhận</option><option value="CLOSED">Đã đóng</option><option value="FALSE_POSITIVE">Phát hiện sai</option></select></label><label>Camera<select v-model="filters.cameraId"><option value="">Tất cả</option><option v-for="camera in cameras" :key="camera.id" :value="camera.id">{{ camera.name }}</option></select></label><label>Địa điểm<select v-model="filters.placeId"><option value="">Tất cả</option><option v-for="place in places" :key="place.id" :value="place.id">{{ place.name }}</option></select></label><label>Hành vi<select v-model="filters.behaviorCode"><option value="">Tất cả</option><option v-for="behavior in behaviors" :key="behavior.id" :value="behavior.code">{{ behavior.nameVi }}</option></select></label><label>Mức độ<select v-model="filters.severity"><option value="">Tất cả</option><option value="CRITICAL">Nghiêm trọng</option><option value="WARNING">Cảnh báo</option><option value="INFO">Thông tin</option></select></label><label>Từ ngày<input v-model="filters.from" type="datetime-local" /></label><label>Đến ngày<input v-model="filters.to" type="datetime-local" /></label><label>Tìm kiếm<input v-model="filters.q" placeholder="Camera, vùng, hành vi..." /></label><button class="primary-button">Lọc</button></form></div>
  <div class="surface"><div class="card-heading"><h2>Danh sách cảnh báo</h2><span>{{ loading ? 'Đang tải...' : `${alerts.length} dòng / trang` }}</span></div><div class="table-scroll"><table><thead><tr><th>#</th><th>Bắt đầu</th><th>Danh mục hành vi</th><th>Tên vùng</th><th>Camera</th><th>Địa điểm</th><th>Mức độ</th><th>Trạng thái</th></tr></thead><tbody><tr v-for="(item, index) in alerts" :key="item.id" class="click-row" @click="select(item.id)"><td>{{ (page - 1) * 10 + index + 1 }}</td><td>{{ new Date(item.detectedAt).toLocaleString('vi-VN') }}</td><td><strong>{{ item.behaviorName }}</strong><small>{{ item.behaviorCode }} {{ item.behaviorVersion }}</small></td><td>{{ item.zoneId }}</td><td>{{ item.cameraName }}</td><td>{{ item.placeName || '—' }}</td><td><span class="status-pill" :class="item.severity.toLowerCase()">{{ item.severity === 'CRITICAL' ? 'Nghiêm trọng' : item.severity === 'WARNING' ? 'Cảnh báo' : 'Thông tin' }}</span></td><td>{{ item.status }}</td></tr><tr v-if="!alerts.length"><td colspan="8" class="empty">Không có cảnh báo phù hợp.</td></tr></tbody></table></div><div class="pagination"><span>Trang {{ page }} / {{ Math.max(1, Math.ceil(total / 10)) }}</span><div><button class="secondary-button" :disabled="page <= 1" @click="page--; load()">Trước</button><button class="secondary-button" :disabled="page * 10 >= total" @click="page++; load()">Sau</button></div></div></div>
  <EventDetail :alert-id="selected" :refresh-tick="detailTick" @close="closeDetail" @changed="load" />
</template>
