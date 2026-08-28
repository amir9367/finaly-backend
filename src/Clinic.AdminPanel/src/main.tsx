import React from 'react'
import ReactDOM from 'react-dom/client'
import { ConfigProvider } from 'antd'
import faIR from 'antd/locale/fa_IR'
import App from './App'
import './styles.css'

// HealthySpace — dark premium palette (#171717 / #79E19B / #FFFFFF)
const theme = {
  token: {
    colorPrimary: '#79E19B',
    colorInfo: '#79E19B',
    colorLink: '#79E19B',
    colorSuccess: '#79E19B',
    colorBgBase: '#171717',
    borderRadius: 16,
    fontFamily: "Figtree, Vazirmatn, Vazir, 'Segoe UI', Tahoma, sans-serif",
    colorTextBase: '#FFFFFF',
  },
  components: {
    Table: { headerBg: '#1E1E1E', headerColor: '#A1A1A1', rowHoverBg: 'rgba(121,225,155,0.06)', borderColor: 'rgba(255,255,255,0.06)' },
    Card: { colorBgContainer: '#1E1E1E' },
    Modal: { contentBg: '#1E1E1E', headerBg: '#1E1E1E', titleColor: '#FFFFFF' },
    Select: { colorBgContainer: '#242424', colorBorder: 'rgba(255,255,255,0.08)' },
    Input: { colorBgContainer: '#242424', colorBorder: 'rgba(255,255,255,0.08)', activeBorderColor: '#79E19B' },
  },
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={faIR} direction="rtl" theme={theme}>
      <App />
    </ConfigProvider>
  </React.StrictMode>,
)
