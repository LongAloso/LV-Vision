import axios from 'axios';

export const http = axios.create({ timeout: 20000 });
http.interceptors.request.use(config => {
  const token = sessionStorage.getItem('lv-vision-token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export function errorText(error: unknown): string {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.message || error.response?.data?.detail || error.message;
  }
  return error instanceof Error ? error.message : 'Không thể hoàn tất yêu cầu';
}

export async function evidenceUrl(path: string): Promise<string> {
  const response = await http.get(path, { responseType: 'blob' });
  return URL.createObjectURL(response.data);
}
