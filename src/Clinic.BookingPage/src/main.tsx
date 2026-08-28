import React from 'react'
import ReactDOM from 'react-dom/client'
import { ConfigProvider } from 'antd'
import faIR from 'antd/locale/fa_IR'
import App from './App'
import './styles.css'

// HealthySpace premium palette (#79E19B / #171717 / #FFFFFF)
const theme = {
  token: {
    colorPrimary: '#79E19B',
    colorInfo: '#79E19B',
    colorLink: '#171717',
    colorSuccess: '#79E19B',
    borderRadius: 16,
    fontFamily: "Figtree, Vazirmatn, Vazir, 'Segoe UI', Tahoma, sans-serif",
  },
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={faIR} direction="rtl" theme={theme}>
      <App />
    </ConfigProvider>
  </React.StrictMode>,
)
