<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { http, errorText } from '../../../core/api/http';
import type { Health } from '../../../core/api/types';
import { can } from '../../../core/auth/session';

const health = ref<Health | null>(null); const error = ref(''); const busy = ref('');
let timer: number | null = null;
async function load() {
  try { health.value = (await http.get<Health>('/systemservice/system/health')).data; error.value = ''; }
  catch (cause) { error.value = errorText(cause); }
}
async function resetReference(cameraId: string) {
  busy.value = cameraId;
  try { await http.post(`/cameraservice/cameras/${cameraId}/reset-reference`); await load(); }
  catch (cause) { error.value = errorText(cause); }
  finally { busy.value = ''; }
}
onMounted(() => { load(); timer = window.setInterval(load, 5000); });
onUnmounted(() => { if (timer) window.clearInterval(timer); });
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">QUẢN TRỊ HỆ THỐNG</div><h1>Tình trạng hệ thống</h1><p>Trạng thái dịch vụ, camera, worker và hàng đợi Redis.</p></div><button class="secondary-button" @click="load"><i class="pi pi-refresh" /> Làm mới</button></section>
  <div v-if="error" class="error-box">{{ error }}</div>
  <template v-if="health">
    <div class="resource-grid"><div v-for="item in [['CPU', health.resources.cpuPercent], ['RAM', health.resources.memoryPercent], ['Đĩa', health.resources.diskPercent]]" :key="item[0]" class="surface resource-card"><span>{{ item[0] }}</span><strong>{{ Number(item[1]).toFixed(1) }}%</strong><div class="meter"><span :style="{ width: `${Math.min(100, Number(item[1]))}%` }" /></div></div><div class="surface resource-card"><span>GPU</span><strong>Chưa có số liệu</strong><small>Worker CPU hiện hành</small></div></div>
    <p class="subtle resource-scope">Số liệu CPU/RAM do container nhìn thấy trên Docker host; GPU và PLC chưa có telemetry trong Phase 2.</p>
    <div class="health-columns"><div class="surface"><div class="card-heading"><h2>Dịch vụ</h2><span>{{ health.services.filter(item => item.status === 'OK').length }}/{{ health.services.length }} bình thường</span></div><div class="table-scroll"><table><thead><tr><th>Dịch vụ</th><th>Trạng thái</th><th>Chi tiết</th><th>Phản hồi</th></tr></thead><tbody><tr v-for="item in health.services" :key="item.name"><td><strong>{{ item.name }}</strong></td><td><span class="status-pill" :class="item.status === 'OK' ? 'ok' : 'critical'">{{ item.status === 'OK' ? 'Bình thường' : 'Lỗi' }}</span></td><td>{{ item.detail }}</td><td>{{ item.latencyMs === null ? '—' : `${item.latencyMs} ms` }}</td></tr></tbody></table></div></div><div class="surface"><div class="card-heading"><h2>Hàng đợi Redis</h2><span>Độ dài Stream</span></div><div class="table-scroll"><table><thead><tr><th>Stream</th><th>Số tin</th><th>Ghi chú</th></tr></thead><tbody><tr v-for="item in health.redisQueues" :key="item.name"><td><code>{{ item.name }}</code></td><td>{{ item.length ?? '—' }}</td><td>Độ dài lưu trữ, chưa phải backlog consumer</td></tr></tbody></table></div></div></div>
    <div class="surface"><div class="card-heading"><h2>Camera và worker</h2><span>{{ health.cameras.length }} camera · {{ health.workers.length }} worker</span></div><div class="table-scroll"><table><thead><tr><th>Camera</th><th>Trạng thái</th><th>FPS</th><th>p50 / p95</th><th>Worker</th><th>Thao tác</th></tr></thead><tbody><tr v-for="camera in health.cameras" :key="camera.id"><td><strong>{{ camera.name }}</strong><small>{{ camera.placeName || 'Chưa gán địa điểm' }}</small></td><td><span class="status-pill" :class="camera.fps > 0 ? 'ok' : 'muted'">{{ camera.fps > 0 ? 'Hoạt động' : 'Chưa chạy' }}</span></td><td>{{ camera.fps.toFixed(1) }} / {{ camera.targetFps }}</td><td>{{ camera.p50Ms.toFixed(0) }} / {{ camera.p95Ms.toFixed(0) }} ms</td><td>{{ camera.workerId || '—' }}</td><td><button v-if="can('Camera.Manage')" class="text-button" :disabled="busy === camera.id" @click="resetReference(camera.id)">Đặt lại ảnh tham chiếu</button></td></tr><tr v-if="!health.cameras.length"><td colspan="6" class="empty">Chưa có camera.</td></tr></tbody></table></div></div>
    <div class="surface"><div class="card-heading"><h2>Thiết bị PLC</h2><span>Phase 4</span></div><p class="empty">Chưa cấu hình thiết bị PLC; không hiển thị trạng thái giả.</p></div>
  </template>
</template>
