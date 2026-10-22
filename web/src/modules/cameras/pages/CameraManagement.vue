<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { http, errorText } from '../../../core/api/http';
import type { Camera, Page, Place } from '../../../core/api/types';
import { can } from '../../../core/auth/session';

const route = useRoute(); const router = useRouter();
const cameras = ref<Camera[]>([]); const places = ref<Place[]>([]);
const running = ref<string[]>([]); const error = ref(''); const busy = ref('');
const editing = ref<Camera | 'new' | null>(null);
const deleting = ref<Camera | null>(null);
const form = reactive({ name: '', rtspUrl: '', placeId: '', targetFps: 5 });

async function load() {
  try {
    const [cameraResult, placeResult, streamResult] = await Promise.all([
      http.get<Page<Camera>>('/cameraservice/cameras?pageSize=100'),
      http.get<Page<Place>>('/cameraservice/places?pageSize=100'),
      http.get<{ items: { cameraId: string; running: boolean }[] }>('/detect/sitting/streams')
        .catch(() => ({ data: { items: [] as { cameraId: string; running: boolean }[] } })),
    ]);
    cameras.value = cameraResult.data.items;
    places.value = placeResult.data.items;
    running.value = streamResult.data.items.filter(item => item.running).map(item => item.cameraId);
  } catch (cause) { error.value = errorText(cause); }
}
function openForm(camera: Camera | 'new') {
  editing.value = camera; error.value = '';
  Object.assign(form, camera === 'new'
    ? { name: '', rtspUrl: '', placeId: '', targetFps: 5 }
    : { name: camera.name, rtspUrl: '', placeId: camera.placeId || '', targetFps: camera.targetFps });
}
async function save() {
  const camera = editing.value;
  if (!camera) return;
  error.value = ''; busy.value = 'save';
  try {
    if (camera === 'new') {
      const { data } = await http.post<Camera>('/cameraservice/cameras', {
        name: form.name.trim(), rtspUrl: form.rtspUrl.trim(),
        placeId: form.placeId || null, targetFps: Number(form.targetFps),
      });
      editing.value = null;
      await load();
      await router.push({ path: '/scenarios', query: { cameraId: data.id } });
      return;
    }
    const wasRunning = running.value.includes(camera.id);
    if (wasRunning) await http.post('/detect/sitting/streams/stop', { cameraId: camera.id });
    try {
      await http.patch(`/cameraservice/cameras/${camera.id}`, {
        name: form.name.trim(), placeId: form.placeId || null,
        targetFps: Number(form.targetFps),
        ...(form.rtspUrl.trim() ? { rtspUrl: form.rtspUrl.trim() } : {}),
      });
    } catch (cause) {
      if (wasRunning) await http.post('/detect/sitting/streams/start', { cameraId: camera.id });
      throw cause;
    }
    if (wasRunning) await http.post('/detect/sitting/streams/start', { cameraId: camera.id });
    editing.value = null;
    await load();
  } catch (cause) { error.value = errorText(cause); }
  finally { busy.value = ''; }
}
async function remove() {
  const camera = deleting.value;
  if (!camera) return;
  error.value = ''; busy.value = 'delete';
  const wasRunning = running.value.includes(camera.id);
  try {
    if (wasRunning) {
      await http.post('/detect/sitting/streams/stop', { cameraId: camera.id });
    }
    try { await http.delete(`/cameraservice/cameras/${camera.id}`); }
    catch (cause) {
      if (wasRunning) {
        try { await http.post('/detect/sitting/streams/start', { cameraId: camera.id }); }
        catch { /* report the original delete error; load() below can refresh worker state */ }
      }
      throw cause;
    }
    deleting.value = null;
    await load();
  } catch (cause) { error.value = errorText(cause); await load(); }
  finally { busy.value = ''; }
}
watch(() => route.query.add, value => { if (value === '1' && can('Camera.Manage')) openForm('new'); }, { immediate: true });
onMounted(load);
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">THIẾT BỊ & VỊ TRÍ</div><h1>Camera</h1><p>Quản lý thiết bị RTSP. Kịch bản và vùng được cấu hình riêng sau khi thêm camera.</p></div><button v-if="can('Camera.Manage')" class="primary-button" @click="openForm('new')"><i class="pi pi-plus" /> Thêm camera</button></section>
  <div v-if="error" class="error-box" role="alert">{{ error }}</div>
  <div class="surface"><div class="card-heading"><h2>Danh sách thiết bị</h2><span>{{ cameras.length }} camera</span></div><div class="table-scroll"><table><thead><tr><th>Tên camera</th><th>Địa điểm</th><th>Trạng thái</th><th>FPS mục tiêu</th><th>Worker</th><th>Thao tác</th></tr></thead><tbody><tr v-for="camera in cameras" :key="camera.id"><td><strong>{{ camera.name }}</strong><small>{{ camera.id }}</small></td><td>{{ places.find(item => item.id === camera.placeId)?.name || 'Chưa gán' }}</td><td><span class="status-pill" :class="running.includes(camera.id) ? 'ok' : 'muted'">{{ running.includes(camera.id) ? 'Đang chạy' : 'Chưa chạy' }}</span></td><td>{{ camera.targetFps }}</td><td>{{ camera.workerId || '—' }}</td><td class="actions"><button class="text-button" @click="router.push({ path: '/scenarios', query: { cameraId: camera.id } })">Kịch bản & vùng</button><button v-if="can('Camera.Manage')" class="text-button" @click="openForm(camera)">Sửa</button><button v-if="can('Camera.Manage')" class="text-button danger" @click="deleting = camera">Xóa</button></td></tr><tr v-if="!cameras.length"><td colspan="6" class="empty">Chưa có camera.</td></tr></tbody></table></div></div>
  <div v-if="editing" class="modal-backdrop" @click.self="editing = null"><form class="modal" @submit.prevent="save"><div class="card-heading"><h2>{{ editing === 'new' ? 'Thêm camera' : `Sửa ${editing.name}` }}</h2><button type="button" class="icon-button" aria-label="Đóng" @click="editing = null"><i class="pi pi-times" /></button></div><label>Tên camera<input v-model="form.name" maxlength="160" required /></label><label>{{ editing === 'new' ? 'RTSP URL' : 'RTSP URL mới (để trống nếu giữ nguyên)' }}<input v-model="form.rtspUrl" type="url" pattern="rtsps?://.*" placeholder="rtsp://..." :required="editing === 'new'" /></label><p v-if="editing !== 'new'" class="subtle">URL cũ không hiển thị vì có thể chứa mật khẩu.</p><label>Địa điểm<select v-model="form.placeId"><option value="">Chưa gán</option><option v-for="place in places" :key="place.id" :value="place.id">{{ place.name }}</option></select></label><label>FPS mục tiêu<input v-model.number="form.targetFps" type="number" min="5" max="10" required /></label><div class="info-box">Thêm camera không tự gán hành vi. Sau khi lưu, bạn chọn kịch bản và vẽ vùng cho camera này.</div><button class="primary-button" :disabled="busy === 'save'">{{ editing === 'new' ? 'Thêm camera' : 'Lưu thay đổi' }}</button></form></div>
  <div v-if="deleting" class="modal-backdrop" @click.self="deleting = null"><div class="modal"><div class="card-heading"><h2>Xóa camera?</h2><button class="icon-button" aria-label="Đóng" @click="deleting = null"><i class="pi pi-times" /></button></div><p>Camera <strong>{{ deleting.name }}</strong> và các kịch bản gắn với nó sẽ bị xóa. Cảnh báo đã ghi vẫn được giữ làm lịch sử.</p><div class="form-row"><button class="secondary-button" @click="deleting = null">Hủy</button><button class="primary-button danger-button" :disabled="busy === 'delete'" @click="remove">Xóa camera</button></div></div></div>
</template>
