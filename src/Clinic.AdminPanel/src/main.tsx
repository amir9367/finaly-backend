import React from 'react'
import ReactDOM from 'react-dom/client'
import { ConfigProvider } from 'antd'
import faIR from 'antd/locale/fa_IR'
import App from './App'
import './styles.css'

// Persian-clinic identity: pine ink, saffron accent, warm paper (light theme)
const theme = {
  token: {
    colorPrimary: '#0B4F4A',
    colorInfo: '#2FA79B',
    colorLink: '#0B4F4A',
    colorSuccess: '#2FA79B',
    colorError: '#B3402F',
    colorWarning: '#E8A33D',
    colorBgBase: '#F7F5F0',
    colorBgContainer: '#FFFFFF',
    colorText: '#1E2B29',
    colorTextSecondary: '#63716D',
    colorBorder: '#E5E0D4',
    colorBorderSecondary: '#EFEAE0',
    borderRadius: 12,
    fontFamily: "Vazirmatn, 'Segoe UI', Tahoma, sans-serif",
  },
  components: {
    Table: {
      headerBg: '#EAF3F1',
      headerColor: '#0B4F4A',
      rowHoverBg: '#E4F2F0',
      borderColor: '#EFEAE0',
    },
    Modal: { titleColor: '#0B4F4A' },
    Select: { colorBorder: '#E5E0D4' },
    Input: { colorBorder: '#E5E0D4', activeBorderColor: '#0B4F4A' },
  },
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={faIR} direction="rtl" theme={theme}>
      <App />
    </ConfigProvider>
  </React.StrictMode>,
)
