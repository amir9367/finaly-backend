import { useState } from 'react'
import {
  Alert,
  Button,
  Card,
  Space,
  Statistic,
  Table,
  Typography,
  Upload,
  message,
  type UploadProps,
} from 'antd'
import { DownloadOutlined, ExportOutlined, UploadOutlined } from '@ant-design/icons'
import { api, downloadFile, errMsg } from '../api'
import type { ImportResult } from '../types'

export default function ExcelPage() {
  const [result, setResult] = useState<ImportResult | null>(null)
  const [busy, setBusy] = useState(false)

  const downloadTemplate = async () => {
    try {
      await downloadFile('/admin/excel/template', 'clinic-appointments-template.xlsx')
      message.success('قالب دانلود شد')
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  const exportExcel = async () => {
    try {
      await downloadFile('/admin/excel/export', 'clinic-appointments.xlsx')
      message.success('خروجی آماده شد')
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  const importFile: UploadProps['customRequest'] = async (options) => {
    const formData = new FormData()
    formData.append('file', options.file as File)
    setBusy(true)
    try {
      const response = await api.post<ImportResult>('/admin/excel/import', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      setResult(response.data)
      message.success('ورود اطلاعات انجام شد')
      options.onSuccess?.(response.data)
    } catch (error) {
      message.error(errMsg(error))
      options.onError?.(error as Error)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="page-card">
      <Card title="همگام‌سازی اکسل">
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div className="toolbar-row">
            <Button icon={<DownloadOutlined />} onClick={downloadTemplate}>
              دانلود قالب
            </Button>
            <Button icon={<ExportOutlined />} onClick={exportExcel}>
              خروجی از سیستم
            </Button>
          </div>

          <Upload.Dragger
            accept=".xlsx"
            showUploadList={false}
            customRequest={importFile}
            disabled={busy}
          >
            <p className="ant-upload-drag-icon">
              <UploadOutlined />
            </p>
            <p className="ant-upload-text">فایل قالب را اینجا رها کنید یا کلیک کنید</p>
            <p className="ant-upload-hint">فقط .xlsx — ردیف‌های مشکل‌دار گزارش می‌شوند، بی‌صدا حذف نمی‌شوند</p>
          </Upload.Dragger>

          {result && (
            <>
              <Space size="large">
                <Statistic title="مجموع ردیف‌ها" value={result.totalRows} />
                <Statistic title="ثبت جدید" value={result.imported} valueStyle={{ color: '#3f8600' }} />
                <Statistic title="به‌روزرسانی" value={result.updated} />
                <Statistic
                  title="رد شده"
                  value={result.skipped}
                  valueStyle={result.skipped > 0 ? { color: '#cf1322' } : undefined}
                />
              </Space>

              {result.errors.length > 0 && (
                <Alert
                  type="warning"
                  showIcon
                  message={`${result.errors.length} ردیف با مشکل مواجه شد`}
                  description={
                    <Table
                      size="small"
                      rowKey={(row) => `${row.row}-${row.error}`}
                      dataSource={result.errors}
                      pagination={{ pageSize: 5 }}
                      columns={[
                        { title: 'ردیف فایل', dataIndex: 'row', width: 110 },
                        { title: 'مشکل', dataIndex: 'error' },
                      ]}
                    />
                  }
                />
              )}

              <Typography.Text type="secondary">
                شناسه ثبت همگام‌سازی: {result.syncLogId}
              </Typography.Text>
            </>
          )}
        </Space>
      </Card>
    </div>
  )
}
