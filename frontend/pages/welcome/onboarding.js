import OnBoarding from "../../components/onBoarding";
import getFlag from "../../lib/getFlag";
import OnBoardingStore from "../../components/onBoarding/store";

const OnBoardingPage = () => {
    if (!getFlag('onBoardingPageEnabled', false)) return null;
    return null;
    return <OnBoardingStore.Provider>
        <OnBoarding></OnBoarding>
    </OnBoardingStore.Provider>
}

OnBoardingPage.getInitialProps = () => {
    return {
        title: 'Onboarding - Averia',
    }
}

export default OnBoardingPage;