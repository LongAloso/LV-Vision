<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { http, errorText } from '../../../core/api/http';
import type { Behavior, Page } from '../../../core/api/types';
import { can } from '../../../core/auth/session';

const rows = ref<Behavior[]>([]); const total = ref(0); const page = ref(1);
const q = ref(''); const active = ref('');
const error = ref(''); const busy = ref(false);
const editing = ref<Behavior | null | 'new'>(null);
const history = ref<Behavior[] | null>(null);
const historyCode = ref('');
const draft = reactive({ code: '', nameVi: '', nameEn: '', detectTemplate: 'POSTURE_STATE',
  severity: 'WARNING', zoneType: '', parameters: '{}', isActive: true });
async function load() {
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: '10' });
    if (q.value) params.set('q', q.value);
    if (active.value) params.set('isActive', active.value);
    const { data } = await http.get<Page<Behavior>>(`/alertservice/behaviors?${params}`);
    rows.value = data.items; total.value = data.total;
  } catch (cause) { error.value = errorText(cause); }
}
function openEdit(item: Behavior | 'new') {
  editing.value = item; error.value = '';
  Object.assign(draft, item === 'new'
    ? { code: '', nameVi: '', nameEn: '', detectTemplate: 'POSTURE_STATE', severity: 'WARNING',
        zoneType: '', parameters: '{}', isActive: true }
    : { code: item.code, nameVi: item.nameVi, nameEn: item.nameEn,
        detectTemplate: item.detectTemplate, severity: item.severity, zoneType: item.zoneType,
        parameters: JSON.stringify(item.parameters, null, 2), isActive: item.isActive });
}
async function save() {
  if (!editing.value) return;
  busy.value = true; error.value = '';
  try {
    const parameters = JSON.parse(draft.parameters);
    if (!parameters || Array.isArray(parameters) || typeof parameters !== 'object') throw new Error('Tham số phải là object JSON');
    const body = { ...draft, parameters };
    if (editing.value === 'new') await http.post('/alertservice/behaviors', body);
    else await http.put(`/alertservice/behaviors/${editing.value.id}`, body);
    editing.value = null; await load();
  } catch (cause) { error.value = errorText(cause); }
  finally { busy.value = false; }
}
async function clone(item: Behavior) {
  busy.value = true; error.value = '';
  try { await http.post(`/alertservice/behaviors/${item.id}/clone`); await load(); }
  catch (cause) { error.value = errorText(cause); }
  finally { busy.value = false; }
}
async function showHistory(item: Behavior) {
  error.value = '';
  try {
    history.value = (await http.get(`/alertservice/behaviors/${item.id}/history`)).data.items;
    historyCode.value = item.code;
  } catch (cause) { error.value = errorText(cause); }
}
async function deactivate(item: Behavior) {
  if (!confirm(`Tắt ${item.code} ${item.version}? Cảnh báo mới cho mã này sẽ dừng nếu không còn version bật.`)) return;
  error.value = '';
  try { await http.delete(`/alertservice/behaviors/${item.id}`); await load(); }
  catch (cause) { error.value = errorText(cause); }
}
onMounted(load);
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">CẤU HÌNH PHÂN TÍCH</div><h1>Danh mục hành vi</h1><p>Quản lý tên, mức độ và lịch sử phiên bản.</p></div><button v-if="can('Behavior.Manage')" class="primary-button" @click="openEdit('new')"><i class="pi pi-plus" /> Thêm hành vi</button></section>
  <div class="info-box">Sửa tham số tạo version mới trong danh mục; detector chưa tự nhận ngưỡng mới. Cần cập nhật kịch bản và kiểm định AI tại hiện trường.</div>
  <div v-if="error" class="error-box">{{ error }}</div>
  <div class="surface"><div class="filter-bar compact"><label>Tìm hành vi<input v-model="q" placeholder="Mã, tên Việt/Anh" @keyup.enter="page = 1; load()" /></label><label>Trạng thái<select v-model="active" @change="page = 1; load()"><option value="">Tất cả</option><option value="true">Bật</option><option value="false">Tắt</option></select></label><button class="primary-button" @click="page = 1; load()">Tìm kiếm</button></div></div>
  <div class="surface"><div class="card-heading"><h2>Danh mục ({{ total }})</h2><span>Phiên bản mới nhất của mỗi mã</span></div><div class="table-scroll"><table><thead><tr><th>Mã hành vi</th><th>Nhãn</th><th>Tên Việt</th><th>Tên Anh</th><th>Mẫu phát hiện</th><th>Mức độ</th><th>Loại vùng</th><th>Phiên bản</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody><tr v-for="item in rows" :key="item.id"><td><code>{{ item.code }}</code></td><td>{{ item.isSystem ? 'Hệ thống' : 'Tùy chỉnh' }}</td><td><strong>{{ item.nameVi }}</strong></td><td>{{ item.nameEn }}</td><td>{{ item.detectTemplate }}</td><td><span class="status-pill" :class="item.severity.toLowerCase()">{{ item.severity }}</span></td><td>{{ item.zoneType }}</td><td>{{ item.version }}</td><td><span class="status-pill" :class="item.isActive ? 'ok' : 'muted'">{{ item.isActive ? 'Bật' : 'Tắt' }}</span></td><td class="actions"><button v-if="can('Behavior.Manage')" class="text-button" @click="openEdit(item)">Sửa</button><button class="text-button" @click="showHistory(item)">Lịch sử</button><button v-if="can('Behavior.Manage')" class="text-button" :disabled="busy" @click="clone(item)">Nhân bản</button><button v-if="item.isActive && can('Behavior.Manage')" class="text-button danger" @click="deactivate(item)">Tắt</button></td></tr><tr v-if="!rows.length"><td colspan="10" class="empty">Không có hành vi phù hợp.</td></tr></tbody></table></div><div class="pagination"><span>Trang {{ page }} / {{ Math.max(1, Math.ceil(total / 10)) }}</span><div><button class="secondary-button" :disabled="page <= 1" @click="page--; load()">Trước</button><button class="secondary-button" :disabled="page * 10 >= total" @click="page++; load()">Sau</button></div></div></div>
  <div v-if="editing" class="modal-backdrop" @click.self="editing = null"><form class="modal" @submit.prevent="save"><div class="card-heading"><h2>{{ editing === 'new' ? 'Thêm hành vi' : `Sửa ${draft.code} · tạo version mới` }}</h2><button type="button" class="icon-button" @click="editing = null"><i class="pi pi-times" /></button></div><label>Mã hành vi<input v-model="draft.code" :readonly="editing !== 'new'" pattern="[A-Z][A-Z0-9_]+" required /></label><div class="form-row"><label>Tên Việt<input v-model="draft.nameVi" required /></label><label>Tên Anh<input v-model="draft.nameEn" required /></label></div><div class="form-row"><label>Mẫu phát hiện<input v-model="draft.detectTemplate" required /></label><label>Loại vùng<input v-model="draft.zoneType" required /></label></div><label>Mức độ<select v-model="draft.severity"><option>INFO</option><option>WARNING</option><option>CRITICAL</option></select></label><label>Tham số JSON<textarea v-model="draft.parameters" rows="5" required /></label><label class="switch-label"><input v-model="draft.isActive" type="checkbox" /> Bật phiên bản mới</label><button class="primary-button" :disabled="busy">Lưu</button></form></div>
  <div v-if="history" class="modal-backdrop" @click.self="history = null"><div class="modal"><div class="card-heading"><h2>Lịch sử {{ historyCode }}</h2><button class="icon-button" @click="history = null"><i class="pi pi-times" /></button></div><div v-for="item in history" :key="item.id" class="history-item"><strong>{{ item.version }} · {{ item.nameVi }}</strong><span>{{ item.severity }} · {{ item.isActive ? 'Bật' : 'Tắt' }}</span><pre>{{ JSON.stringify(item.parameters, null, 2) }}</pre></div></div></div>
</template>
