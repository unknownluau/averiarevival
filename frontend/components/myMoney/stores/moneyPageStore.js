import { useState } from "react";
import { createContainer } from "unstated-next";
import { getTradeStyle, tradePageStyle } from "../../../services/theme";

const MoneyPageStore = createContainer(() => {
  const [tab, setTab] = useState(null);

  return {
    tab,
    setTab,
    getUrl: (tab) => {
      switch (tab) {
        case 'Trade Items': 
          return getTradeStyle() === tradePageStyle.Modern ? '/trades' : '/My/Trades.aspx';
        default:
          return '/My/Money.aspx'
      }
    }
  }
});

export default MoneyPageStore;