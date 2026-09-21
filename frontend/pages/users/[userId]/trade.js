import NewTradePage from "../../../components/newTradePage";

const UserTradeRoute = () => {
  return <NewTradePage />;
};

UserTradeRoute.getInitialProps = () => {
  return {
    title: "Trade - Averia",
  };
};

export default UserTradeRoute;
