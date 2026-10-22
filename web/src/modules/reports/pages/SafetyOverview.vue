<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { http, errorText } from '../../../core/api/http';
import type { ReportSummary } from '../../../core/api/types';
import { liveAlertTick } from '../../../core/websocket/live';

const report = ref<ReportSummary | null>(null);
const error = ref('');
const loading = ref(false);
const peak = computed(() => Math.max(1, ...report.value?.daily.map(item => item.count) || [0]));
async function load() {
  loading.value = true;
  try { report.value = (await http.get<ReportSummary>('/reportservice/statistics')).data; error.value = ''; }
  catch (cause) { error.value = errorText(cause); }
  finally { loading.value = false; }
}
watch(liveAlertTick, load);
onMounted(load);
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">BÁO CÁO & TRA CỨU</div><h1>Tổng quan an toàn</h1><p>Cảnh báo phát hiện trong 30 ngày gần nhất, tính theo ngày UTC.</p></div><button class="secondary-button" :disabled="loading" @click="load"><i class="pi pi-refresh" /> Làm mới</button></section>
  <div v-if="error" class="error-box" role="alert">{{ error }}</div>
  <template v-if="report">
    <div class="report-kpis"><div v-for="item in [['Tổng cảnh báo', report.total], ['Đang xử lý', report.open], ['Nghiêm trọng', report.critical], ['Phát hiện sai', report.falsePositive]]" :key="item[0]" class="surface report-kpi"><span>{{ item[0] }}</span><strong>{{ item[1] }}</strong></div></div>
    <div class="surface"><div class="card-heading"><h2>Cảnh báo theo ngày</h2><span>{{ report.from }} → {{ report.to }} · UTC</span></div><div v-if="report.total" class="report-bars"><div v-for="day in report.daily" :key="day.day" class="report-bar" :title="`${day.day}: ${day.count} cảnh báo`"><span>{{ day.count || '' }}</span><div :style="{ height: `${Math.max(2, day.count / peak * 100)}%` }" /><small>{{ day.day.slice(5) }}</small></div></div><p v-else class="empty">Chưa có cảnh báo trong khoảng này.</p></div>
    <div class="report-columns"><div class="surface"><div class="card-heading"><h2>Theo mức độ</h2></div><div class="table-scroll"><table><tbody><tr v-for="item in report.bySeverity" :key="item.severity"><td>{{ item.severity }}</td><td><strong>{{ item.count }}</strong></td></tr><tr v-if="!report.bySeverity.length"><td colspan="2" class="empty">Chưa có dữ liệu.</td></tr></tbody></table></div></div><div class="surface"><div class="card-heading"><h2>Camera có cảnh báo</h2></div><div class="table-scroll"><table><tbody><tr v-for="item in report.byCamera" :key="`${item.id}-${item.name}`"><td>{{ item.name }}</td><td><strong>{{ item.count }}</strong></td></tr><tr v-if="!report.byCamera.length"><td colspan="2" class="empty">Chưa có dữ liệu.</td></tr></tbody></table></div></div></div>
  </template>
</template>
