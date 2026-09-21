import {createUseStyles} from "react-jss";
import NewModal from "../../newModal";
import ActionButton from "../../actionButton";
import GroupsPageStore from "../stores/GroupsPageStore";
import AuthenticationStore from "../../../stores/authentication";
import FeedbackStore from "../../../stores/feedback";
import useButtonStyles from "../../../styles/buttonStyles";
import {leaveGroup} from "../../../services/groups-typed";
import { FeedbackType } from "../../../models/feedback";
import { wait } from "../../../lib/utils";

const useStyles = createUseStyles({
    btn: {
        padding: 9,
        margin: '0 6px',
        minWidth: 90,
        display: 'inline-block',
        fontSize: 16,
        lineHeight: '100%',
        fontWeight: 500,
    },
    container: {
        "& > span": {
            marginBottom: 12,
        },
    },
});

const LeaveGroupModal = ({ExitFunction}) => {
    const s = useStyles();
    const buttonStyles = useButtonStyles();
    const auth = AuthenticationStore.useContainer();
    const store = GroupsPageStore.useContainer();
    const feedback = FeedbackStore.useContainer();

    return <NewModal
        title='Leave Group'
        exitFunction={ExitFunction}
        headerBorder={true}
        children={<div className={`${s.container} flex flex-column`}>
            <span>Are you sure you want to leave this group?</span>
            <div className={`flex w-100 justify-content-center align-items-center`}>
                <ActionButton
                    label='Yes'
                    onClick={async () => {
                        if (ExitFunction) ExitFunction();
                        try {
                            await leaveGroup({ groupId: store.group?.id, userId: auth.userId });
                            feedback.addFeedback(`You have left ${store.group?.name}`, FeedbackType.SUCCESS, true);
                            await wait(3);
                            window.location.reload();
                        } catch (e) {
                            console.error(e);
                            feedback.addFeedback(`Could not leave group: ${e?.message}`, FeedbackType.ERROR, true);
                        }
                    }}
                    buttonStyle={buttonStyles.newContinueButton}
                    className={s.btn}
                />
                <ActionButton
                    label='No'
                    onClick={ExitFunction}
                    buttonStyle={buttonStyles.newContinueButton}
                    className={s.btn}
                />
            </div>
        </div>}
    />
}

export default LeaveGroupModal;