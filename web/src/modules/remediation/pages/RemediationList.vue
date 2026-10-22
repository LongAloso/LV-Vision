<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { http, errorText } from '../../../core/api/http';
import type { Page, Remediation } from '../../../core/api/types';

interface RemediationRow extends Remediation { alertTitle: string; cameraName: string; status: string }
const router = useRouter();
const items = ref<RemediationRow[]>([]); const page = ref(1); const total = ref(0); const error = ref('');
async function load() {
  try {
    const { data } = await http.get<Page<RemediationRow>>(`/alertservice/remediations?page=${page.value}&pageSize=10`);
    items.value = data.items; total.value = data.total;
  } catch (cause) { error.value = errorText(cause); }
}
onMounted(load);
</script>

<template>
  <section class="page-head"><div><div class="eyebrow">GIÁM SÁT / XỬ LÝ</div><h1>Hành động khắc phục</h1><p>Các việc đã ghi trên cảnh báo và kết quả xử lý.</p></div><span class="count-chip">{{ total }} hành động</span></section>
  <div v-if="error" class="error-box">{{ error }}</div>
  <div class="surface"><div class="table-scroll"><table><thead><tr><th>Thời gian</th><th>Cảnh báo</th><th>Camera</th><th>Mô tả</th><th>Người làm</th><th>Kết quả</th><th>Trạng thái</th></tr></thead><tbody><tr v-for="item in items" :key="item.id" class="click-row" @click="router.push({ path: '/alerts', query: { id: item.alertId } })"><td>{{ new Date(item.createdAt).toLocaleString('vi-VN') }}</td><td><strong>{{ item.alertTitle }}</strong></td><td>{{ item.cameraName }}</td><td>{{ item.description }}</td><td>{{ item.person }}</td><td>{{ item.result }}</td><td>{{ item.status }}</td></tr><tr v-if="!items.length"><td colspan="7" class="empty">Chưa có hành động khắc phục.</td></tr></tbody></table></div><div class="pagination"><span>Trang {{ page }} / {{ Math.max(1, Math.ceil(total / 10)) }}</span><div><button class="secondary-button" :disabled="page <= 1" @click="page--; load()">Trước</button><button class="secondary-button" :disabled="page * 10 >= total" @click="page++; load()">Sau</button></div></div></div>
</template>
