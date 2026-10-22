<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { http, errorText } from '../../../core/api/http';
import type { Camera, Page, ReportSummary } from '../../../core/api/types';

const report = ref<ReportSummary | null>(null);
const cameras = ref<Camera[]>([]);
const error = ref('');
const loading = ref(false);
const exporting = ref(false);
const appliedQuery = ref('');
const filters = reactive({ from: '', to: '', cameraId: '', behaviorCode: '', severity: '' });
function query() {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) if (value) params.set(key, value);
  return params.toString();
}
async function load() {
  loading.value = true;
  const selectedQuery = query();
  try { report.value = (await http.get<ReportSummary>(`/reportservice/statistics?${selectedQuery}`)).data; appliedQuery.value = selectedQuery; error.value = ''; }
  catch (cause) { error.value = errorText(cause); }
  finally { loading.value = false; }
}
async function exportExcel() {
  exporting.value = true; error.value = '';
  try {
    const response = await http.get(`/reportservice/statistics/export?${appliedQuery.value}`,
      { responseType: 'blob', timeout: 120000 });
    const url = URL.createObjectURL(response.data);
    const anchor = document.createElement('a'); anchor.href = url;
    anchor.download = `lv-vision-alerts-${report.value?.from || 'from'}-${report.value?.to || 'to'}.xlsx`;
    anchor.click(); window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (cause) {
    const blob = (cause as { response?: { data?: Blob } }).response?.data;
    if (blob instanceof Blob && blob.type.includes('json')) {
      try { error.value = JSON.parse(await blob.text()).message || 'Không thể xuất Excel'; }
      catch { error.value = errorText(cause); }
    } else error.value = errorText(cause);
  }
  finally { exporting.value = false; }
}
onMounted(async () => {
  await load();
  try { cameras.value = (await http.get<Page<Camera>>('/cameraservice/cameras?pageSize=100')).data.items; }
  catch { /* report still supports other filters */ }
});
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">BÁO CÁO & TRA CỨU</div><h1>Báo cáo thống kê</h1><p>Lọc theo ngày UTC, camera, hành vi và mức độ; xuất danh sách cảnh báo ra Excel.</p></div><button class="primary-button" :disabled="exporting || loading" @click="exportExcel"><i class="pi pi-file-excel" /> {{ exporting ? 'Đang xuất...' : 'Xuất Excel' }}</button></section>
  <div v-if="error" class="error-box" role="alert">{{ error }}</div>
  <div class="surface"><form class="filter-bar" @submit.prevent="load"><label>Từ ngày UTC<input v-model="filters.from" type="date" /></label><label>Đến ngày UTC<input v-model="filters.to" type="date" /></label><label>Camera<select v-model="filters.cameraId"><option value="">Tất cả</option><option v-for="item in cameras" :key="item.id" :value="item.id">{{ item.name }}</option></select></label><label>Mã hành vi<input v-model.trim="filters.behaviorCode" placeholder="Ví dụ PERSON_SITTING" /></label><label>Mức độ<select v-model="filters.severity"><option value="">Tất cả</option><option value="CRITICAL">Nghiêm trọng</option><option value="WARNING">Cảnh báo</option><option value="INFO">Thông tin</option></select></label><button class="primary-button" :disabled="loading">{{ loading ? 'Đang tải...' : 'Lọc' }}</button></form></div>
  <template v-if="report">
    <p class="subtle">{{ report.from }} → {{ report.to }} · {{ report.total }} cảnh báo · {{ report.critical }} nghiêm trọng · {{ report.open }} đang xử lý</p>
    <div class="report-columns"><div class="surface"><div class="card-heading"><h2>Theo hành vi</h2></div><div class="table-scroll"><table><thead><tr><th>Hành vi</th><th>Số cảnh báo</th></tr></thead><tbody><tr v-for="item in report.byBehavior" :key="`${item.code}-${item.name}`"><td><strong>{{ item.name }}</strong><small>{{ item.code }}</small></td><td>{{ item.count }}</td></tr><tr v-if="!report.byBehavior.length"><td colspan="2" class="empty">Chưa có dữ liệu.</td></tr></tbody></table></div></div><div class="surface"><div class="card-heading"><h2>Theo camera</h2></div><div class="table-scroll"><table><thead><tr><th>Camera</th><th>Số cảnh báo</th></tr></thead><tbody><tr v-for="item in report.byCamera" :key="`${item.id}-${item.name}`"><td>{{ item.name }}</td><td>{{ item.count }}</td></tr><tr v-if="!report.byCamera.length"><td colspan="2" class="empty">Chưa có dữ liệu.</td></tr></tbody></table></div></div></div>
    <div class="report-columns"><div class="surface"><div class="card-heading"><h2>Theo mức độ</h2></div><div class="table-scroll"><table><tbody><tr v-for="item in report.bySeverity" :key="item.severity"><td>{{ item.severity }}</td><td>{{ item.count }}</td></tr><tr v-if="!report.bySeverity.length"><td colspan="2" class="empty">Chưa có dữ liệu.</td></tr></tbody></table></div></div><div class="surface"><div class="card-heading"><h2>Theo trạng thái</h2></div><div class="table-scroll"><table><tbody><tr v-for="item in report.byStatus" :key="item.status"><td>{{ item.status }}</td><td>{{ item.count }}</td></tr><tr v-if="!report.byStatus.length"><td colspan="2" class="empty">Chưa có dữ liệu.</td></tr></tbody></table></div></div></div>
  </template>
</template>
