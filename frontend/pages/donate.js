import Donate from "../components/donate";
import Theme2016 from "../components/theme2016";

const DonatePage = () => {
    return <Theme2016>
        <Donate/> 
    </Theme2016>;
};

DonatePage.getInitialProps = () => {
    return {
        title: 'Donate - Averia',
    };
};

export default DonatePage;
